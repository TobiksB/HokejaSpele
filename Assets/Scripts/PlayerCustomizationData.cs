using UnityEngine;

[System.Serializable]
public class PlayerCustomizationData
{
    public float skinColorValue;
    public int hairStyle;
    public int beardStyle;
    public int eyebrowStyle;

    public static PlayerCustomizationData Current = new PlayerCustomizationData();

    public void SaveToPrefs()
    {
        PlayerPrefs.SetFloat("SkinColor", skinColorValue);
        PlayerPrefs.SetInt("HairStyle", hairStyle);
        PlayerPrefs.SetInt("BeardStyle", beardStyle);
        PlayerPrefs.SetInt("EyebrowStyle", eyebrowStyle);
        PlayerPrefs.Save();
    }

    public void LoadFromPrefs()
    {
        skinColorValue = PlayerPrefs.GetFloat("SkinColor", 1.0f);
        hairStyle = PlayerPrefs.GetInt("HairStyle", 0);
        beardStyle = PlayerPrefs.GetInt("BeardStyle", 0);
        eyebrowStyle = PlayerPrefs.GetInt("EyebrowStyle", 0);
    }
}
