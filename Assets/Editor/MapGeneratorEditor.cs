﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor {
	public override void OnInspectorGUI() {
		DrawDefaultInspector();

		MapGenerator generator = (MapGenerator)target;

		if (GUILayout.Button("Create module data")) {
			generator.CreateModules();
		}
		if (GUILayout.Button("Simplify module data")) {
			generator.CreateModules();
			generator.SimplifyNeighborData();
		}

		GUILayout.Space(20f);

		if (GUILayout.Button("Clear")) {
			generator.Clear();
		}
		if (GUILayout.Button("Initialize")) {
			generator.Initialize();
			generator.CollapseDefaultArea(true);
			generator.BuildAllSlots();
			Debug.Log("Map initialized.");
		}
	}
}
