// from: http://wordpress.notargs.com/blog/blog/2015/11/07/unity5quaternion%E3%82%92%E3%82%AA%E3%82%A4%E3%83%A9%E3%83%BC%E8%A7%92%E3%81%A7%E6%89%B1%E3%81%86%E3%81%9F%E3%82%81%E3%81%AE%E3%82%A8%E3%83%87%E3%82%A3%E3%82%BF%E6%8B%A1%E5%BC%B5%E3%82%92%E4%BD%9C/

namespace Osakana4242.UnityEditorUtil {
	using UnityEngine;
	using UnityEditor;

	[CustomPropertyDrawer(typeof(Quaternion))]
	public class QuaternionEditor : PropertyDrawer {

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			Vector3 euler = property.quaternionValue.eulerAngles;
			euler = EditorGUI.Vector3Field(position, label, euler);
			property.quaternionValue = Quaternion.Euler(euler);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			return Screen.width < 333 ? (16f + 18f) : 16f;
		}
	}
}
