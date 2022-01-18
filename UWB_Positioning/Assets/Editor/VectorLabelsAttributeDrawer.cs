using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VectorLabelsAttribute))]
public class VectorLabelsAttributeDrawer : PropertyDrawer {
    private static readonly GUIContent[] defaultLabels = new GUIContent[] { new GUIContent("X"), new GUIContent("Y"), new GUIContent("Z"), new GUIContent("W") };

    private const int twoLinesThreshold = 375;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        int factor = Screen.width < twoLinesThreshold ? 2 : 1;
        return factor * base.GetPropertyHeight(property, label);
    }

    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        VectorLabelsAttribute vectorLabels = (VectorLabelsAttribute)attribute;

        if (property.propertyType == SerializedPropertyType.Vector2Int) {
            int[] array = new int[] { property.vector2IntValue.x, property.vector2IntValue.y };
            array = DrawFields(position, array, ObjectNames.NicifyVariableName(property.name), EditorGUI.IntField, vectorLabels);
            property.vector2IntValue = new Vector2Int(array[0], array[1]);
        }
        else if (property.propertyType == SerializedPropertyType.Vector3Int) {
            int[] array = new int[] { property.vector3IntValue.x, property.vector3IntValue.y, property.vector3IntValue.z };
            array = DrawFields(position, array, ObjectNames.NicifyVariableName(property.name), EditorGUI.IntField, vectorLabels);
            property.vector3IntValue = new Vector3Int(array[0], array[1], array[2]);
        }
        else if (property.propertyType == SerializedPropertyType.Vector2) {
            float[] array = new float[] { property.vector2Value.x, property.vector2Value.y };
            array = DrawFields(position, array, ObjectNames.NicifyVariableName(property.name), EditorGUI.FloatField, vectorLabels);
            property.vector2Value = new Vector2(array[0], array[1]);
        }
        else if (property.propertyType == SerializedPropertyType.Vector3) {
            float[] array = new float[] { property.vector3Value.x, property.vector3Value.y, property.vector3Value.z };
            array = DrawFields(position, array, ObjectNames.NicifyVariableName(property.name), EditorGUI.FloatField, vectorLabels);
            property.vector3Value = new Vector3(array[0], array[1], array[2]);
        }
        else if (property.propertyType == SerializedPropertyType.Vector4) {
            float[] array = new float[] { property.vector4Value.x, property.vector4Value.y, property.vector4Value.z, property.vector4Value.w };
            array = DrawFields(position, array, ObjectNames.NicifyVariableName(property.name), EditorGUI.FloatField, vectorLabels);
            property.vector4Value = new Vector4(array[0], array[1], array[2]);
        }
    }

    private T[] DrawFields<T>(Rect rect, T[] vector, string mainLabel, System.Func<Rect, GUIContent, T, T> fieldDrawer, VectorLabelsAttribute vectorLabels) {
        T[] result = vector;

        bool twoLinesLayout = Screen.width < twoLinesThreshold;

        // Get the rect of the main label
        Rect mainLabelRect = rect;
        mainLabelRect.width = EditorGUIUtility.labelWidth;
        if (twoLinesLayout)
            mainLabelRect.height *= 0.5f;

        // Get the size of each field rect
        Rect fieldRect = rect;
        if (twoLinesLayout) {
            fieldRect.height *= 0.5f;
            fieldRect.y += fieldRect.height;
            fieldRect.width = rect.width / vector.Length;
        }
        else {
            fieldRect.x += mainLabelRect.width;
            fieldRect.width = (rect.width - mainLabelRect.width) / vector.Length;
        }

        EditorGUI.LabelField(mainLabelRect, mainLabel);

        for (int i = 0; i < vector.Length; i++) {
            GUIContent label = vectorLabels.Labels.Length > i ? new GUIContent(vectorLabels.Labels[i]) : defaultLabels[i];
            Vector2 labelSize = EditorStyles.label.CalcSize(label);
            EditorGUIUtility.labelWidth = Mathf.Max(labelSize.x + 5, 0.3f * fieldRect.width);
            result[i] = fieldDrawer(fieldRect, label, vector[i]);
            fieldRect.x += fieldRect.width;
        }

        EditorGUIUtility.labelWidth = 0;

        return result;
    }
}