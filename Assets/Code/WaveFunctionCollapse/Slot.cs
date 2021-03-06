using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Slot {
	public Vector3i Position;

	// List of modules that can still be placed here
	public ModuleSet Modules;

	// Direction -> Module -> Number of items in this.getneighbor(direction).Modules that allow this module as a neighbor
	public int[][] ModuleHealth;
	
	private MapGenerator mapGenerator;

	private IMap map;

	public Module Module;

	public GameObject GameObject;

	public bool Collapsed {
		get {
			return this.Module != null;
		}
	}

	public int Entropy {
		get {
			return this.Modules.Count;
		}
	}

	public Slot(Vector3i position, MapGenerator mapGenerator, IMap map) {
		this.Position = position;
		this.mapGenerator = mapGenerator;
		this.map = map;
		this.Modules = new ModuleSet(initializeFull: true);
	}

	public Slot(Vector3i position, MapGenerator mapGenerator, Slot prototype) : this(position, mapGenerator, mapGenerator) {
		this.ModuleHealth = prototype.ModuleHealth.Select(a => a.ToArray()).ToArray();
		this.Modules = new ModuleSet(prototype.Modules);
	}

	// TODO only look up once and then cache???
	public Slot GetNeighbor(int direction) {
		return this.map.GetSlot(this.Position + Orientations.Direction[direction]);
	}

	public void Collapse(Module module) {
		if (this.Collapsed) {
			Debug.LogWarning("Trying to collapse already collapsed slot.");
			return;
		}

		this.mapGenerator.History.Push(new HistoryItem());

		this.Module = module;
		var toRemove = new ModuleSet(this.Modules);
		toRemove.Remove(module);
		this.RemoveModules(toRemove);

		this.mapGenerator.MarkSlotComplete(this);
	}

	private void checkConsistency(Module module) {
		for (int d = 0; d < 6; d++) {
			if (this.GetNeighbor(d) != null && this.GetNeighbor(d).Collapsed && !this.GetNeighbor(d).Module.PossibleNeighbors[(d + 3) % 6].Contains(module)) {
				this.mark(2f, Color.red);
				// This would be a result of inconsistent code, should not be possible.
				throw new Exception("Illegal collapse, not in neighbour list. (Incompatible connectors)");
			}
		}

		if (!this.Modules.Contains(module)) {
			this.mark(2f, Color.red);
			// This would be a result of inconsistent code, should not be possible.
			throw new Exception("Illegal collapse!");
		}
	}

	public void CollapseRandom() {
		if (!this.Modules.Any()) {
			throw new CollapseFailedException(this);
		}
		if (this.Collapsed) {
			throw new Exception("Slot is already collapsed.");
		}
		
		float max = this.Modules.Select(module => module.Prototype.Probability).Sum();
		float roll = (float)(MapGenerator.Random.NextDouble() * max);
		float p = 0;
		foreach (var candidate in this.Modules) {
			p += candidate.Prototype.Probability;
			if (p >= roll) {
				this.Collapse(candidate);
				return;
			}
		}
		this.Collapse(this.Modules.First());
	}

	public static int RemoveCalls = 0;

	public void RemoveModules(ModuleSet modulesToRemove) {
		var affectedNeighbouredModules = Enumerable.Range(0, 6).Select(_ => new ModuleSet()).ToArray();

		foreach (var module in modulesToRemove) {
			if (!this.Modules.Contains(module) || module == this.Module) {
				continue;
			}
			if (this.mapGenerator.History != null && this.mapGenerator.History.Any()) {
				this.mapGenerator.History.Peek().RemoveModule(this, module);
			}
			for (int d = 0; d < 6; d++) {
				int inverseDirection = (d + 3) % 6;
				var neighbor = this.GetNeighbor(d);
				if (neighbor == null) {
					continue;
				}

				foreach (var possibleNeighbor in module.PossibleNeighbors[d]) {
					if (neighbor.ModuleHealth[inverseDirection][possibleNeighbor.Index] == 1) {
						affectedNeighbouredModules[d].Add(possibleNeighbor);
					}
#if UNITY_EDITOR
					if (neighbor.ModuleHealth[inverseDirection][possibleNeighbor.Index] < 1) {
						throw new System.InvalidOperationException("ModuleHealth must not be negative.");
					}
#endif
					neighbor.ModuleHealth[inverseDirection][possibleNeighbor.Index]--;
				}
			}
			this.Modules.Remove(module);
		}

		if (this.Modules.Count == 0) {
			throw new CollapseFailedException(this);
		}

		for (int d = 0; d < 6; d++) {
			if (affectedNeighbouredModules[d].Any() && this.GetNeighbor(d) != null && !this.GetNeighbor(d).Collapsed) {
				this.GetNeighbor(d).RemoveModules(affectedNeighbouredModules[d]);
			}
		}
	}


	/// <summary>
	/// Add modules non-recursively.
	/// Returns true if this lead to this slot changing from collapsed to not collapsed.
	/// </summary>
	public bool AddModules(ModuleSet modulesToAdd) {
		foreach (var module in modulesToAdd) {
			if (this.Modules.Contains(module) || module == this.Module) {
				continue;
			}
			for (int d = 0; d < 6; d++) {
				int inverseDirection = (d + 3) % 6;
				var neighbor = this.GetNeighbor(d);
				if (neighbor == null) {
					continue;
				}

				foreach (var possibleNeighbor in module.PossibleNeighbors[d]) {
					neighbor.ModuleHealth[inverseDirection][possibleNeighbor.Index]++;
				}
			}
			this.Modules.Add(module);
		}

		if (this.Collapsed && this.Modules.Count > 1) {
			this.Module = null;
			this.mapGenerator.MarkSlotIncomplete(this);
			return true;
		}
		return false;
	}

	private void mark(float size, Color color) {
		var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		cube.transform.parent = this.mapGenerator.transform;
		cube.transform.localScale = Vector3.one * size;
		cube.GetComponent<MeshRenderer>().sharedMaterial.color = color;
		cube.transform.position = this.GetPosition();
	}

	public bool Build() {
		if (this.GameObject != null) {
#if UNITY_EDITOR
			GameObject.DestroyImmediate(this.GameObject);
#else
			GameObject.Destroy(this.GameObject);
#endif
		}

		if (!this.Collapsed || this.Module.Prototype.Spawn == false) {
			return false;
		}		

		var gameObject = GameObject.Instantiate(this.Module.Prototype.gameObject);
		gameObject.name = this.Module.Prototype.gameObject.name + " " + this.Position;
		GameObject.DestroyImmediate(gameObject.GetComponent<ModulePrototype>());
		gameObject.transform.parent = this.mapGenerator.transform;
		gameObject.transform.position = this.GetPosition();
		gameObject.transform.rotation = Quaternion.Euler(Vector3.up * 90f * this.Module.Rotation);
		var blockBehaviour = gameObject.AddComponent<BlockBehaviour>();
		blockBehaviour.Slot = this;
		this.GameObject = gameObject;
		return true;
	}

	public Vector3 GetPosition() {
		return this.mapGenerator.GetWorldspacePosition(this.Position);
	}

	public void EnforceConnector(int direction, int connector) {
		var toRemove = this.Modules.Where(module => !module.Fits(direction, connector));
		this.RemoveModules(ModuleSet.FromEnumerable(toRemove));
	}

	public void ExcludeConnector(int direction, int connector) {
		var toRemove = this.Modules.Where(module => module.Fits(direction, connector));
		this.RemoveModules(ModuleSet.FromEnumerable(toRemove));
	}

	public override int GetHashCode() {
		return this.Position.GetHashCode();
	}
}
