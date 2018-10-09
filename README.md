## FlatSharp

FlatSharp is Google's FlatBuffers serialization format implemented in C#, for C#. FlatBuffers is a zero-copy binary serialization format intended for high-performance scenarios. FlatSharp leverages the latest and greatest from .NET in the form of ```Memory<T>``` and ```Span<T>```. As such, FlatSharp's safe-code implementations are competitive (and in some cases, faster) than other unsafe implementations. 

### Current Status
FlatSharp is a very new project and is in active development. There are no known uses in production environments at this time. The current code can be considered alpha quality. Contributions and proposals are always welcomed. Currently, FlatSharp supports the following FlatBuffers features:
- Structs
- Tables
- Scalars / Strings
- ```IList<T>```/```IReadOnlyList<T>``` Vectors of Strings, Tables, Structs, and Scalars
- ```Memory<T>```/```ReadOnlyMemory<T>``` Vectors of scalars when on little-endian systems (1-byte scalars are allowed in Memory vectors on big-endian systems)

Enums are not currently supported for schema-compatibility reasons; it's too easy to make an implicit change to the type of an enum (going from ```MyEnum : byte``` -> ```MyEnum : int```), which results in a FlatBuffer binary break. Adding enum support may be considered in the future. FlatBuffer unions are also not supported.

### License
FlatSharp is a C# implementation of Google's FlatBuffer binary format, which is licensed under the Apache 2.0 License. Accordingly, FlatSharp is also licensed under Apache 2.0. FlatSharp incorporates code from the Google FlatSharp library for testing and benchmarking purposes.

### Getting Started
FlatSharp uses C# as its schema, and does not require any additional files or build-time code generation. Like many other serializers, the process is to annotate your data contracts with attributes, and you're on your way.

#### Defining a Contract

```C#
[FlatBufferTable]
public class MonsterTable
{
    [FlatBufferItem(0)]
    public virtual Position Position { get; set; }
    
    [FlatBufferItem(1)]
    [DefaultValue(150s)]
    public virtual short Mana { get; set; }
    
    [FlatBufferItem(2)]
    [DefaultValue(100s)]
    public virtual short HP { get; set; }
    
    [FlatBufferItem(3)]
    public virtual string Name { get; set; }
    
    [FlatBufferItem(4, IsDeprecated = true)]
    public virtual bool Friendly { get; set; }
    
    [FlatBufferItem(5)]
    public virtual ReadOnlyMemory<byte> Inventory { get; set; }
    
    [FlatBufferItem(6)]
    [DefaultValue((int)Color.Blue)]
    public virtual int RawColor { get; set; }
    
    // Expressing enums still possible.
    public Color Color
    {
      get => (Color)this.RawColor;
      set => this.RawColor = (int)value;
    }
}

[FlatBufferStruct]
public class Position
{
   [FlatBufferItem(0)]
   public virtual Float X { get; set; }
   
   [FlatBufferItem(1)]
   public virtual Float Y { get; set; }
   
   [FlatBufferItem(2)]
   public virtual Float Z { get; set; }
}
```
For FlatSharp to be able to work with your schema, it must obey the following set of contraints:
- All types must be public and externally visible
- All types must be unsealed.
- All properties decorated by ```[FlatSharpItem]``` must be virtual and public. Setters may be omitted, but Getters are required.
- All FlatSharpItem indexes must be unique within the given data type.
- Struct/Table vectors must be defined as ```IList<T>``` or ```IReadOnlyList<T>```.
- Scalar vectors must be defined as either ```IList<T>```, ```IReadOnlyList<T>```, ```Memory<T>```, or ```ReadOnlyMemory<T>```.
- All types must be serializable in FlatBuffers (that is -- you can't throw in an arbitrary C# type).

When versioning your schema, the [FlatBuffer rules apply](https://google.github.io/flatbuffers/flatbuffers_guide_writing_schema.html)

#### Serializing and Deserializing
```C#
public void ReadMonsterMemory(Memory<byte> monsterBuffer)
{
  MonsterTable monster = FlatBufferSerializer.Default.Parse<MonsterTable>(new MemoryInputBuffer(monsterBuffer));
  Console.WriteLine($"{monster.Position.X}, {monster.Position.Y}, {monster.Position.Z}");
}

public void ReadMonsterArray(byte[] monsterBuffer)
{
  MonsterTable monster = FlatBufferSerializer.Default.Parse<MonsterTable>(new ArrayInputBuffer(monsterBuffer));
  Console.WriteLine($"{monster.Position.X}, {monster.Position.Y}, {monster.Position.Z}");
}

public void WriteMonster(MonsterTable monster)
{
  // FlatSharp does not allocate memory for you when serializing. You may get a BufferTooSmall exception
  // in cases where the supplied buffer was not long enough to hold the data. The recommendation is to
  // pool serialization buffers in a way that makes sense for you.
  byte[] monsterBytes = new byte[10 * 1024];
  FlatBufferSerializer.Default.Serialize(monster, buffer.AsSpan());
}
```

### Internals
FlatSharp works by generating dynamic subclasses of your data contracts based on the schema that you define, which is why they must be public and virtual. That is, when you attempt to deserialize a ```MonsterTable``` object, you actually get back a dynamic subclass of ```MonsterTable```, which has properties defined in such a way as to index into the buffer. When a FlatSharp object reads a value for it, it goes ahead and makes a copy of that value so that it does not need to consult the original buffer again.

### Safety
FlatSharp is a lazy parser. That is -- data from the underlying buffer is not actually parsed until you request it. This keeps things very lean throughout your application and prevents your application from paying a deserialize tax on items that you will not use. However, this is a double-edged sword, and any changes to the underlying buffer will modify, and possibly corrupt, the state of any objects that reference that buffer.

```C#
public void ReadMonster(byte[] monsterBuffer)
{
  MonsterTable monster = FlatBufferSerializer.Default.Parse<MonsterTable>(monsterBuffer);
  monsterBuffer[7] = 0;
  
  // This data is no longer valid (and accessing these properties may throw)
  Console.WriteLine($"{monster.Position.X}, {monster.Position.Y}, {monster.Position.Z}");
}
```
Therefore, to use FlatSharp effectively, you must do so with buffer lifecycle management in mind. The simplest way to accomplish is to just let the GC take care of it for you. However, in scenarios where buffers are pooled, lifecycle management becomes important.

In its default configuration, FlatSharp uses no unsafe code at all, and only uses overflow-checked operators. FlatSharp does come with an unsafe companion library that can meaningfully improve performance in some scenarios (```UnsafeMemoryInputBuffer``` provides roughly double the performance of ```MemoryInputBuffer``` with the caveat that it must be disposed for performance to be acceptable).

### Performance & Benchmarks
FlatSharp is really fast. This is primarily thanks to new changes in C# with Memory and Span, as well as FlatBuffers itself exposing a very simple type system that makes optimization simple. FlatSharp has a default serializer instance (```FlatBuffersSerializer.Default```), however it is possible to tune the serializer by creating your own with a custom ```FlatBufferSerializerOptions``` instance. Right now, the only option is ```CacheListVectorData```, which instructs FlatSharp to generate List Vector deserializers that progressively caches data as it is read. This costs an additional array allocation, and is slower in the case when each element is accessed only once. However, when each element is accessed multiple times, this is a useful optimization.

The FlatSharp benchmarks were run on .NET Core 2.1, using a C# approximation of [Google's FlatBuffer benchmark](https://github.com/google/flatbuffers/tree/benchmarks/benchmarks/cpp/FB). The FlatSharp benchmarks use this schema, but with the following parameters:
- Vector length = 3 or 30
- Traversal count = 1 or 5

The benchmarks test 4 different serialization frameworks:
- FlatSharp (of course :))
- Protobuf.NET
- Google's C# Flatbuffers implementation
- ZeroFormatter

#### Serialization
![image](doc/Serialization.png)

#### Deserialization + 1 Traversal of Data
![image](doc/Deserialization_1_Traversal.png)

#### Deserialization + 5 Traversals of Data
![image](doc/Deserialization_5_Traversal.png)

### Roadmap
- Support for property setters
- Support for enums?
- Security hardening and fuzzing
- Code gen based on FBS schema files
- GRPC support