using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Medusa
{
	[CustomEditor(typeof(IvyGenerator))]
	[CanEditMultipleObjects]
	public class IvyGeneratorEditor : Editor 
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			if (GUILayout.Button("Grow"))
			{
				(target as IvyGenerator).Grow();
			}
		}
	}
}

