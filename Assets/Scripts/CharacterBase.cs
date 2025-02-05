using UnityEngine;
using System.Collections.Generic;

public class CharacterBase : MonoBehaviour
{
    public bool IsInitialized { get; protected set; }

    protected virtual void Awake()
    {
        IsInitialized = true;
    }

    protected virtual void Start() {}

    public virtual void ChangeSkinColor(Color color) {}
    
    public virtual void ChangeHairstyle(int style) {}
    
    public virtual void ChangeBeardstyle(int style) {}
    
    public virtual void ChangeEyebrowstyle(int style) {}

    public virtual List<string> GetAvailableHairStyles() { return new List<string>(); }
    
    public virtual List<string> GetAvailableBeardStyles() { return new List<string>(); }
    
    public virtual List<string> GetAvailableEyebrowStyles() { return new List<string>(); }
}
