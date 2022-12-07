using UnityEngine;
using System;

[Serializable]
public struct Vector3S {
    public float x;
    public float y;
    public float z;
 
    public Vector3S(float newX, float newY, float newZ) {
        x = newX;
        y = newY;
        z = newZ;
    }
 
    public override bool Equals(object obj) {
        if (!(obj is Vector3S)) {
            return false;
        }
 
        var s = (Vector3S)obj;
        return x == s.x &&
                y == s.y &&
                z == s.z;
    }
 
    public override int GetHashCode() {
        var hashCode = 373119288;
        hashCode = hashCode * -1521134295 + x.GetHashCode();
        hashCode = hashCode * -1521134295 + y.GetHashCode();
        hashCode = hashCode * -1521134295 + z.GetHashCode();
        return hashCode;
    }
 
    public Vector3 ToVector3() {
        return new Vector3(x, y, z);
    }
 
    public static bool operator ==(Vector3S a, Vector3S b) {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }
 
    public static bool operator !=(Vector3S a, Vector3S b) {
        return a.x != b.x && a.y != b.y && a.z != b.z;
    }
 
    public static implicit operator Vector3(Vector3S x) {
        return new Vector3(x.x, x.y, x.z);
    }
 
    public static implicit operator Vector3S(Vector3 x) {
        return new Vector3S(x.x, x.y, x.z);
    }
}