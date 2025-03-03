using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HockeyNumber : MonoBehaviour {
    public int numberMaterialIndex;
	// Use this for initialization
	void Start () {
        GetComponent<Renderer>().materials[numberMaterialIndex].SetTexture("_MainTex", Resources.Load<Texture2D>("CustomizableCharacters/HockeyNumbers/" + GetComponentInParent<HockeyPlayer>().number));
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
