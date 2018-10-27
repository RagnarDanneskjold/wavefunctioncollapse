﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AreaSelector))]
public class AreaSelectorEditor : Editor {
	public override void OnInspectorGUI() {
		DrawDefaultInspector();

		AreaSelector selector = (AreaSelector)target;
		if (GUILayout.Button("Generate")) {
			selector.MapGenerator.Collapse(selector.StartPosition, selector.Size);
			selector.MapGenerator.BuildAllSlots();
		}
	}
}