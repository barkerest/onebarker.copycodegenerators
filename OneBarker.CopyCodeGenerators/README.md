# Code Generators for Boilerplate Value Copying Methods

Value copying can be tedious.  Sure if your type only has 1-2 properties/fields, it's not a big
deal, but when you start getting into dozens of types with numerous properties, it might
become an issue.

Include this library with the following reference (using the appropriate version):

```xml
<PackageReference Include="OneBarker.CopyCodeGenerators" Version="X.X.X" ExcludeAssets="all" IncludeAssets="analyzers" />
```

Then you can use the [EnableCopyFrom], [EnableInitFrom], [EnableUpdateFrom], [EnableCopyTo], and [EnableUpdateTo]
attributes in your code.

You can also use the [SkipOnCopy] attribute to mark properties/fields to be ignored.


## [EnableInitFrom]

```csharp
// MyVector.cs -- your code
namespace Test;

[EnableInitFrom(typeof(System.Numerics.Vector2))]
public partial record MyVector
{
    public MyVector(float x, float y) {
        X = x;
        Y = y;
    }
    
    public float X { get; init; }
    public float Y { get; init; }
}

// MyVector.g.cs -- generated code
namespace Test;

partial record MyVector
{
    partial void ConstructTransform_X(ref float value);
    partial void ConstructTransform_Y(ref float value);
    partial void AfterConstruct(System.Numerics.Vector2 source);
    
    public MyVector(System.Numerics.Vector2 source)
    {
        var source_X = source.X;
        ConstructTransform_X(ref source_X);
        X = source_X;
        
        var source_Y = source.Y;
        ConstructTransform_Y(ref source_Y);
        Y = source_Y;
        
        AfterConstruct(source);
    }
}
```

The first thing to note, EnableInitFrom will work with record classes or non-record classes.
The generated partial will match your selection.

The next thing to note, we declare several partial methods that you can optionally define.
If you need to transform values, you simply define the ```partial void ConstructTransform_{{PropertyName}}```
method.  If you don't define the method, the compiler will remove the call site.
If you need to process additional data after the construct has copied matching properties, you
simply define the ```partial void AfterConstruct``` method.  Just as before, if you don't define
the method, the compiler will remove the call site.  In the above example, the compiled 
constructor effectively becomes:
```csharp
public MyVector(System.Numerics.Vector2 source)
{
    var source_X = source.X;
    X = source_X;
    
    var source_Y = source.Y;
    Y = source_Y;
}
```

## [EnableCopyFrom]
```csharp
// MyVector.cs -- your code
namespace Test;

[EnableCopyFrom(typeof(System.Numerics.Vector2))]
public partial class MyVector
{
    public float X { get; init; }
    public float Y { get; init; }
}

// MyVector.g.cs -- generated code
namespace Test;

partial class MyVector
{
    partial void CopyFromTransform_X(ref float value);
    partial void CopyFromTransform_Y(ref float value);
    partial void BeforeCopyFrom(System.Numerics.Vector2 source);
    partial void AfterCopyFrom(System.Numerics.Vector2 source);
    
    public new MyVector CopyFrom(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) return this;
        if (ReferenceEquals(this, source)) return this;
        
        BeforeCopyFrom(source);
        
        var source_X = source.X;
        CopyFromTransform_X(ref source_X);
        X = source_X;
        
        var source_Y = source.Y;
        CopyFromTransform_Y(ref source_Y);
        Y = source_Y;
        
        AfterCopyFrom(source);
        
        return this;
    }
}
```

Unlike InitFrom, CopyFrom will not work against a record class.  Records are meant to be
read-only, so copying values from another class after construction does not make sense.

CopyFrom also brings along the Transform partials and After partial, it also adds a Before
partial.  Any of these can be defined to customize behavior, but if not defined the compiler
will remove the call sites.

The CopyFrom method is defined with the 'new' modifier.  This means that if you inherit from 
MyVector into a new class MyPosition and use the EnableCopyFrom attribute again, each
implementation will have a specific version of the CopyFrom method.

```csharp
MyVector v = new MyPosition();
v.CopyFrom(new System.Numerics.Vector2(1,1));
```
In this example, we create an instance of MyPosition but store it in a variable typed as
MyVector.  When we call CopyFrom, the version defined in MyVector will be used because of
the 'new' modifier.  This is the opposite of how 'override' works and may be confusing.
However, I believe it to be the desired behavior since we are treating the value as MyVector.

## EnableUpdateFrom
```csharp
// MyVector.cs -- your code
namespace Test;

[EnableUpdateFrom(typeof(System.Numerics.Vector2))]
public partial class MyVector
{
    public float X { get; init; }
    public float Y { get; init; }
}

// MyVector.g.cs -- generated code
namespace Test;

partial class MyVector
{
    partial void UpdateFromTransform_X(ref float value);
    partial void UpdateFromTransform_Y(ref float value);
    partial void BeforeUpdateFrom(System.Numerics.Vector2 source, ref int changeCount);
    partial void AfterUpdateFrom(System.Numerics.Vector2 source, ref int changeCount);
    
    public new int UpdateFrom(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) return 0;
        if (ReferenceEquals(this, source)) return 0;
        var changeCount = 0;
        
        BeforeUpdateFrom(source, ref changeCount);
        
        var source_X = source.X;
        UpdateFromTransform_X(ref source_X);
        if (X != source_X)
        {
            X = source_X;
            changeCount++;
        }
        
        var source_Y = source.Y;
        UpdateFromTransform_Y(ref source_Y);
        if (Y != source_Y)
        {
            Y = source_Y;
            changeCount++;
        }
        
        AfterUpdateFrom(source, ref changeCount);
        
        return changeCount;
    }
}
```
Just like CopyFrom, UpdateFrom will not work against a record class.

UpdateFrom brings along the Transform partials, but modifies the Before and After partials.
The UpdateFrom variants take a reference to a change count that they can adjust.

The actual UpdateFrom method only updates fields that have changed and will return the number
of changes made.  The BeforeUpdateFrom and AfterUpdateFrom methods, if defined, should only
increment the change count, but there is nothing stopping the methods from decrementing or
even just setting the change count to a constant.

## Notes

* The attributes can be applied multiple times to your class, and each instance will 
generate overloads taking the type you specified in the attribute.
* The attributes can be supplied with the target class to create methods taking the target class.
* The type supplied to the attribute can be a record, class, struct, or interface.
* The samples shown above are simplified.  
  * The generator code handles reference types differently from value types.
  * The generator disables warnings related to using the ```new``` modifier on the UpdateFrom and
  CopyFrom methods.
  * The generator actually copies the target value before comparing in the UpdateFrom method just
  in case the property getter is not pure.
  This is more important with the reference types since they run through several equality tests.
  * The generator also include comments for the generated methods.

