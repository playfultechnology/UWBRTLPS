using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class Positioning : MonoBehaviour {
    public List<GameObject> tags = new List<GameObject>();
    public List<Vector3> ranges = new List<Vector3>();
    // The x-axis is defined as the direction between the first two anchors
    public Transform[] anchors;
    // Helper classes used to deserialize JSON data
    [Serializable]
    public class JsonParseLink {
        public string a;
        public float r;
    }
    public class JsonParseLinks {
        public string id;
        public JsonParseLink[] links;
    }

    // Start is called before the first frame update
    void Start() {
    }

    /**
     * Calculate position from distances to 3 known anchor points
     * See https://en.wikipedia.org/wiki/True-range_multilateration#Three_Cartesian_dimensions,_three_measured_slant_ranges
     */
    Vector3 RangeToPosition(float r1, float r2, float r3) {
        // Calibration adjustment as measured from testing
        r1 -= .45f;
        r2 -= .60f;
        r3 -= .35f;
        
        // U is the displacement in the x axis between the first two anchors
        float U = (anchors[1].position.x - anchors[0].position.x);
        float V2 = (anchors[2].position.x * anchors[2].position.x) + (anchors[2].position.z * anchors[2].position.z);

        // Trilateration formula
        float x = ((r1 * r1) - (r2 * r2) + (U * U)) / (2 * U);
        float y = ((r1 * r1) - (r3 * r3) + (V2) - (2 * anchors[2].position.x * x)) / (2 * anchors[2].position.z);
        float z = Mathf.Sqrt(Mathf.Abs((r1 * r1) - (x * x) - (y * y)));

        // Assume all anchors lie at the same vertical height
        z = 0;

        return new Vector3(x, z, y);
    }

    // Update is called once per frame
    void Update() {
        for (int i = 0; i < ranges.Count; i++) {
            tags[i].transform.position = RangeToPosition(ranges[i].x, ranges[i].y, ranges[i].z);
        }
    }

    public void DecodeJSONUpdate(string JSON) {
        // Parse the incoming JSON string into an array of links
        JsonParseLinks ll = JsonUtility.FromJson<JsonParseLinks>(JSON);

        // Iterate over the values received
        Debug.Log($"From tag {ll.id}: ");
        int index = -1;
        // Find the index of the tag with this id
        for (int i = 0; i < tags.Count; i++) {
            if (tags[i].name == ll.id) {
                Debug.Log($"Which is index {i}");
                index = i;
            }
        }

        // If the tag ID was found in the list
        if (index > -1) {
            // Get the existing values for this tag
            float x = ranges[index].x, y = ranges[index].y, z = ranges[index].z;
            // Update ranges with new values received
            foreach (JsonParseLink l in ll.links) {
                Debug.Log($"{l.a} {l.r}");
                if (l.a == "1") { x = l.r; }
                if (l.a == "2") { y = l.r; }
                if (l.a == "3") { z = l.r; }
            }
            ranges[index] = new Vector3(x, y, z);
        }
    }
}