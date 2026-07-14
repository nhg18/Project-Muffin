using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Card
{
    public int ID { get; private set; }
    
    public Card(int id)
    {
        ID = id;
    }
}
