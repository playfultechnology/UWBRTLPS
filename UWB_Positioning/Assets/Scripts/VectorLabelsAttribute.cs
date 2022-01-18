using UnityEngine;
public class VectorLabelsAttribute : PropertyAttribute {
    public readonly string[] Labels;

    public VectorLabelsAttribute(params string[] labels) {
        Labels = labels;
    }
}