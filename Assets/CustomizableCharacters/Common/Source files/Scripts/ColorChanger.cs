using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorChanger : MonoBehaviour {
    public enum Type {
        Hair,
        Eyes,
        Mouth,
        Skin
    }

    [System.Serializable]
    public class Changer {
        public Type type;
        public int materialIndex;
        public Renderer rend;
    }
    public List<Changer> changers = new List<Changer>();
	// Use this for initialization
	void Awake () {
		foreach(Changer c in changers) {
            if (c.rend == null) {
                c.rend = GetComponentInChildren<Renderer>();
            }
        }
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
