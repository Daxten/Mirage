# State Synchronization

State synchronization refers to the synchronization of values such as integers, floating point numbers, strings and boolean values belonging to scripts.

State synchronization is done from the Server to remote clients. The local client does not have data serialized to it. It does not need it, because it shares the Scene with the server. However, SyncVar hooks are called on local clients.

Data is not synchronized in the opposite direction - from remote clients to the server. To do this, you need to use Server RPC calls.
-   [SyncVars](SyncVars.md)  
    SyncVars are variables of scripts that inherit from <xref:Mirage.NetworkBehaviour>, which are synchronized from the server to clients. 
-   [SyncLists](SyncLists.md)  
    SyncLists contain lists of values and synchronize data from servers to clients.
-   [SyncDictionary](SyncDictionary.md)  
    A SyncDictionary is an associative array containing an unordered list of key, value pairs.
-   [SyncHashSet](SyncHashSet.md)  
    An unordered set of values that do not repeat.
-   [SyncSortedSet](SyncSortedSet.md)  
    A sorted set of values that do not repeat.

## Sync To Owner

It is often the case when you don't want some player data visible to other players. In the inspector change the "Network Sync Mode" from "Observers" (default) to "Owner" to let Mirage know to synchronize the data only with the owning client.

For example, suppose you are making an inventory system. Suppose player A,B and C are in the same area. There will be a total of 12 objects in the entire network:

* Client A has Player A (himself),  Player B and Player C
* Client B has Player A ,  Player B (himself) and Player C
* Client C has Player A ,  Player B and Player C (himself)
* Server has Player A, Player B, Player C

each one of them would have an Inventory component

Suppose Player A picks up some loot.  The server adds the loot to Player's A inventory,  which would have a [SyncLists](SyncLists.md) of Items. 

By default,  Mirage now has to synchronize player A's inventory everywhere,  that means sending an update message to client A,  client B and client C,  because they all have a copy of Player A. This is wasteful,  Client B and Client C do not need to know about Player's A inventory,  they never see it on screen.  It is also a security problem,  someone could hack the client and display other people's inventory and use it to their advantage.

If you set the "Network Sync Mode"  in the Inventory component to "Owner",  then Player A's inventory will only be synchronized with Client A.  

Now,  suppose instead of 3 people you have 50 people in an area and one of them picks up loot.  It means that instead of sending 50 messages to 50 different clients,  you would only send 1.  This can have a big impact in bandwith in your game.

Other typical use cases include quests,  player's hand in a card game, skills, experience, or any other data you don't need to share with other players.

## Advanced State Synchronization

In most cases, the use of SyncVars is enough for your game scripts to serialize their state to clients. However in some cases you might require more complex serialization code. This page is only relevant for advanced developers who need customized synchronization solutions that go beyond Mirage’s normal SyncVar feature.

## Custom Serialization Functions

To perform your own custom serialization, you can implement virtual functions on <xref:Mirage.NetworkBehaviour> to be used for SyncVar serialization. These functions are:

```cs
public virtual bool OnSerialize(NetworkWriter writer, bool initialState);
```

```cs
public virtual void OnDeserialize(NetworkReader reader, bool initialState);
```

Use the `initialState` flag to differentiate between the first time a game object is serialized and when incremental updates can be sent. The first time a game object is sent to a client, it must include a full state snapshot, but subsequent updates can save on bandwidth by including only incremental changes.


The `OnSerialize` function should return true to indicate that an update should be sent. If it returns true, the dirty bits for that script are set to zero. If it returns false, the dirty bits are not changed. This allows multiple changes to a script to be accumulated over time and sent when the system is ready, instead of every frame.

The `OnSerialize` function is only called for `initialState` or when the <xref:Mirage.NetworkBehaviour> is dirty. A <xref:Mirage.NetworkBehaviour> will only be dirty if a `SyncVar` or `SyncObject` (e.g. `SyncList`) has changed since the last OnSerialize call. After data has been sent the <xref:Mirage.NetworkBehaviour> will not be dirty again until the next `syncInterval` (set in the inspector). A <xref:Mirage.NetworkBehaviour> can also be marked as dirty by manually calling `SetDirtyBit` (this does not bypass the syncInterval limit).
 
Although this works,  it is usually better to let Mirage generate these methods and provide [custom serializers](../DataTypes.md) for your specific field.

## Serialization Flow

Game objects with the Network Identity component attached can have multiple scripts derived from <xref:Mirage.NetworkBehaviour>. The flow for serializing these game objects is:

On the server:
-   Each <xref:Mirage.NetworkBehaviour> has a dirty mask. This mask is available inside `OnSerialize` as `syncVarDirtyBits`
-   Each SyncVar in a <xref:Mirage.NetworkBehaviour> script is assigned a bit in the dirty mask.
-   Changing the value of SyncVars causes the bit for that SyncVar to be set in the dirty mask
-   Alternatively, calling `SetDirtyBit` writes directly to the dirty mask
-   <xref:Mirage.NetworkIdentity> game objects are checked on the server as part of it’s update loop
-   If any <xref:Mirage.NetworkBehaviour>s on a <xref:Mirage.NetworkIdentity> are dirty, then an `UpdateVars` packet is created for that game object
-   The `UpdateVars` packet is populated by calling `OnSerialize` on each <xref:Mirage.NetworkBehaviour> on the game object
-   <xref:Mirage.NetworkBehaviour>s that are not dirty write a zero to the packet for their dirty bits
-   <xref:Mirage.NetworkBehaviour>s that are dirty write their dirty mask, then the values for the SyncVars that have changed
-   If `OnSerialize` returns true for a <xref:Mirage.NetworkBehaviour>, the dirty mask is reset for that <xref:Mirage.NetworkBehaviour> so it does not send again until its value changes.
-   The `UpdateVars` packet is sent to ready clients that are observing the game object

On the client:
-   an `UpdateVars packet` is received for a game object
-   The `OnDeserialize` function is called for each <xref:Mirage.NetworkBehaviour> script on the game object
-   Each <xref:Mirage.NetworkBehaviour> script on the game object reads a dirty mask.
-   If the dirty mask for a <xref:Mirage.NetworkBehaviour> is zero, the `OnDeserialize` function returns without reading any more
-   If the dirty mask is non-zero value, then the `OnDeserialize` function reads the values for the SyncVars that correspond to the dirty bits that are set
-   If there are SyncVar hook functions, those are invoked with the value read from the stream.

So for this script:

```cs
using Mirror;

public class Data : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnInt1Changed))]
    public int int1 = 66;

    [SyncVar]
    public int int2 = 23487;

    [SyncVar]
    public string MyString = "Example string";

    void OnInt1Changed(int oldValue, int newValue)
    {
        // do something here
    }
}
```

The following sample shows the code that is generated by Mirage for the `SerializeSyncVars` function which is called inside <xref:Mirage.NetworkBehaviour>`.OnSerialize`:

```cs
public override bool SerializeSyncVars(NetworkWriter writer, bool initialState)
{
    // Write any SyncVars in base class
    bool written = base.SerializeSyncVars(writer, forceAll);

    if (initialState)
    {
        // The first time a game object is sent to a client, send all the data (and no dirty bits)
        writer.WritePackedUInt32((uint)this.int1);
        writer.WritePackedUInt32((uint)this.int2);
        writer.Write(this.MyString);
        return true;
    }
    else 
    {
        // Writes which SyncVars have changed
        writer.WritePackedUInt64(base.syncVarDirtyBits);

        if ((base.get_syncVarDirtyBits() & 1u) != 0u)
        {
            writer.WritePackedUInt32((uint)this.int1);
            written = true;
        }

        if ((base.get_syncVarDirtyBits() & 2u) != 0u)
        {
            writer.WritePackedUInt32((uint)this.int2);
            written = true;  
        }

        if ((base.get_syncVarDirtyBits() & 4u) != 0u)
        {
            writer.Write(this.MyString);
            written = true;     
        }

        return written;
    }
}
```


The following sample shows the code that is generated by Mirage for the `DeserializeSyncVars` function which is called inside <xref:Mirage.NetworkBehaviour>`.OnDeserialize`:

```cs
public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
{
    // Read any SyncVars in base class
    base.DeserializeSyncVars(reader, initialState);

    if (initialState)
    {
        // The first time a game object is sent to a client, read all the data (and no dirty bits)
        int oldInt1 = this.int1;
        this.int1 = (int)reader.ReadPackedUInt32();
        // if old and new values are not equal, call hook
        if (!base.SyncVarEqual<int>(num, ref this.int1))
        {
            this.OnInt1Changed(num, this.int1);
        }

        this.int2 = (int)reader.ReadPackedUInt32();
        this.MyString = reader.ReadString();
        return;
    }

    int dirtySyncVars = (int)reader.ReadPackedUInt32();
    // is 1st SyncVar dirty
    if ((dirtySyncVars & 1) != 0)
    {
        int oldInt1 = this.int1;
        this.int1 = (int)reader.ReadPackedUInt32();
        // if old and new values are not equal, call hook
        if (!base.SyncVarEqual<int>(num, ref this.int1))
        {
            this.OnInt1Changed(num, this.int1);
        }
    }

    // is 2nd SyncVar dirty
    if ((dirtySyncVars & 2) != 0)
    {
        this.int2 = (int)reader.ReadPackedUInt32();
    }

    // is 3rd SyncVar dirty
    if ((dirtySyncVars & 4) != 0)
    {
        this.MyString = reader.ReadString();
    }
}
```

If a <xref:Mirage.NetworkBehaviour> has a base class that also has serialization functions, the base class functions should also be called.
