using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.Linq;
public class TypeConverter
{
    
    public static byte[] SerializeClientDict_Bytes(Dictionary<int, string> dictionary)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream stream = new MemoryStream())
        {
            formatter.Serialize(stream, dictionary);
            return stream.ToArray();
        }
    }
    public static Dictionary<int, string> DeserializeClientDict_Bytes(byte[] byteArray)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream stream = new MemoryStream(byteArray))
        {
            return (Dictionary<int, string>)formatter.Deserialize(stream);
        }
    }
    public static string SerializeClientDict_String(Dictionary<int, string> dictionary)
    {
        var items = dictionary.Select(kvp => kvp.Key.ToString() + ":" + kvp.Value);
        return string.Join(", ", items);
    }
    public static Dictionary<int, string> DeserializeClientDict_String(string json)
    {
        string[] pairArr = json.Split(", ");
        string[] kvp = new string[2];
        Dictionary<int, string> dict = new Dictionary<int, string>();
        foreach(string pair in pairArr)
        {
            kvp = pair.Split(":");
            dict[Convert.ToInt32(kvp[0])] = kvp[1];
        }
        return dict;
    }
    public static byte[] BytesCombine(byte[] frontArr, byte[] backArr)
    {
        byte[] newArr = new byte[frontArr.Length + backArr.Length];
        Array.Copy(frontArr, 0, newArr, 0, frontArr.Length);
        Array.Copy(backArr, 0, newArr, frontArr.Length, backArr.Length);
        return newArr;
    }
    public static Vector3 ConvertToVec(byte[] bytes, int start)
    {
        float x = BitConverter.ToSingle(bytes, start);
        float y = BitConverter.ToSingle(bytes, start + 4);
        float z = BitConverter.ToSingle(bytes, start + 8);
        return new Vector3(x, y, z);
    }
    public static void ConverToBytes(Vector3 vec3, byte[] bytes, int startIndex)
    {//(원본배열, 원본배열의 복사 시작위치, 복사될배열, 복사될배열의 시작위치, 복사개수)
        Buffer.BlockCopy(BitConverter.GetBytes(vec3.x), 0, bytes, startIndex, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(vec3.y), 0, bytes, startIndex + 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(vec3.z), 0, bytes, startIndex + 8, 4);
    }
}
