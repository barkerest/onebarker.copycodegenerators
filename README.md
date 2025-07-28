# Code Generators for Boilerplate Value Copying Methods

Value copying can be tedious.  Sure if your type only has 1-2 properties/fields, it's not a big
deal, but when you start getting into dozens of types with numerous properties, it might
become an issue.

Include this library with the following reference (using the appropriate version):

```xml
<PackageReference Include="OneBarker.CopyCodeGenerators"
                  Version="X.X.X"
                  ExcludeAssets="all"
                  IncludeAssets="analyzers" />
```

Then you can use the `[EnableCopyFrom]`, `[EnableInitFrom]`, `[EnableUpdateFrom]`, 
`[EnableCopyTo]`, and `[EnableUpdateTarget]` attributes in your code.

You can also use the `[SkipOnCopy]` attribute to mark properties/fields to be ignored.


## EnableInitFrom

The `[EnableInitFrom]` attribute creates a new constructor on the marked object.  
Supported marked types are `class`, `record`, `struct`, and `record struct`.  
Supported source types are `class`, `record`, `struct,` `record struct`, and `interface`.

```csharp
// MyVector.cs -- your code
namespace Test;

[EnableInitFrom(typeof(System.Numerics.Vector2))]
public partial record MyVector(float X, float Y);


// MyVector.g.cs -- generated code
namespace Test;

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial record MyVector
{
    /// <summary>
    /// Transforms the X value and returns the new value.
    /// </summary>
    static float PassthroughTransform_X(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        var value = source.X;
        ConstructTransform_X(ref value);
        return value;
    }

    /// <summary>
    /// Transforms the Y value and returns the new value.
    /// </summary>
    static float PassthroughTransform_Y(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        var value = source.Y;
        ConstructTransform_Y(ref value);
        return value;
    }

    /// <summary>
    /// Transforms the X value before assigning the value to the target.
    /// </summary>
    static partial void ConstructTransform_X(ref float value);

    /// <summary>
    /// Transforms the Y value before assigning the value to the target.
    /// </summary>
    static partial void ConstructTransform_Y(ref float value);

    /// <summary>
    /// Method to run after the Construct method finishes copying values.
    /// </summary>
    partial void AfterConstruct(System.Numerics.Vector2 source);

    /// <summary>
    /// Creates an instance of MyVector with values from the provided object.
    /// </summary>
    public MyVector(System.Numerics.Vector2 source) : this(PassthroughTransform_X(source), PassthroughTransform_Y(source))
    {
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        AfterConstruct(source);
    }
}
```

The generator defines quite a bit of boilerplate for the simple initializer.  The code is very
protective over the target object.  Null checks are automatic, even if they technically shouldn't
be possible.  It also defines some transformation partials to allow you to override and customize
the initialization process.

The `ConstructTransform_` methods are static.  They have to be since they are used before the 
object is initialized.  The `AfterConstruct` method is not static since the object should be fully
initialized at this point.  Since these are all partial methods, if you do not provide an 
implementation for them, the compiler will simply strip them out of the resulting binary.

The actual constructor that is generated is based on the object type.  In this case we used 
a record with positional parameters.  Any non-copy constructor must call the primary constructor,
and so we define the `PassthroughTransform_` methods to accomodate the primary constructor.

The test project goes through many examples including records with positional and non-positional
properties.  The simple rule is that if your type has a parameterless constructor defined, then
it will be called before the generated constructor.


## EnableCopyFrom

The `[EnableCopyFrom]` attribute creates a new `CopyFrom` method in the marked object.  
Supported marked types are `class`, and `struct`.  
Supported source types are `class`, `record`, `struct,` `record struct`, and `interface`.


```csharp
// MyVector.cs -- your code
namespace Test;

[EnableCopyFrom(typeof(System.Numerics.Vector2))]
public partial class MyVector
{
    public float X { get; set; }
    public float Y { get; set; }
}


// MyVector.g.cs -- generated code
namespace Test;

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial class MyVector
{
    /// <summary>
    /// Transforms the X value before assigning the value to the target.
    /// </summary>
    static partial void CopyFromTransform_X(ref float value);

    /// <summary>
    /// Transforms the Y value before assigning the value to the target.
    /// </summary>
    static partial void CopyFromTransform_Y(ref float value);

    /// <summary>
    /// Method to run before the CopyFrom method begins copying values.
    /// </summary>
    partial void BeforeCopyFrom(System.Numerics.Vector2 source);

    /// <summary>
    /// Method to run after the CopyFrom method finishes copying values.
    /// </summary>
    partial void AfterCopyFrom(System.Numerics.Vector2 source);

    /// <summary>
    /// Copies properties from the source object to this object and returns this object.
    /// </summary>
    public new MyVector CopyFrom(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        if (ReferenceEquals(this, source)) return this;
        BeforeCopyFrom(source);
        var source_X = source.X;
        CopyFromTransform_X(ref source_X);
        this.X = source_X;
        var source_Y = source.Y;
        CopyFromTransform_Y(ref source_Y);
        this.Y = source_Y;
        AfterCopyFrom(source);
        return this;
    }
}
```

The `CopyFromTransform_` methods are static.  The `BeforeCopyFrom` and `AfterCopyFrom` methods
are not static.

The generated code still has the null checks and adds in a `this` check, even when technically a match is
impossible (eg - because the source is a struct).  The reasoning was to opt on the side of safety.

The `CopyFrom` method is defined with the 'new' modifier.  This means that if you inherit from 
`MyVector` into a new class `MyPosition` and use the `[EnableCopyFrom]` attribute again, each
implementation will have a specific version of the `CopyFrom` method.  The implementation that 
is called is specific to the data type of the variable doing the call.  

```csharp
MyVector v = new MyPosition();
v.CopyFrom(new System.Numerics.Vector2(1,1));
```

In this example, we create an instance of MyPosition but store it in a variable typed as
MyVector.  When we call CopyFrom, the version defined in MyVector will be used because of
the 'new' modifier.  This is the opposite of how 'override' works and may be confusing.
However, I believe it to be the desired behavior since we are treating the value as MyVector.
As a bonus, I don't have to search for the previous implementation and mark the new one as
an override only when a previous virtual version exists.


## EnableUpdateFrom

The `[EnableUpdateFrom]` attribute creates a new `UpdateFrom` method in the marked object.  
Supported marked types are `class`, and `struct`.  
Supported source types are `class`, `record`, `struct,` `record struct`, and `interface`.


```csharp
// MyVector.cs -- your code
namespace Test;

[EnableUpdateFrom(typeof(System.Numerics.Vector2))]
public partial struct MyVector
{
    public float X;
    public float Y;
}

// MyVector.g.cs -- generated code
namespace Test;

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial struct MyVector
{
    /// <summary>
    /// Transforms the X value before assigning the value to the target.
    /// </summary>
    static partial void UpdateFromTransform_X(ref float value);

    /// <summary>
    /// Transforms the Y value before assigning the value to the target.
    /// </summary>
    static partial void UpdateFromTransform_Y(ref float value);

    /// <summary>
    /// Method to run before the UpdateFrom method begins copying values.
    /// </summary>
    partial void BeforeUpdateFrom(System.Numerics.Vector2 source, ref int changeCount);

    /// <summary>
    /// Method to run after the UpdateFrom method finishes copying values.
    /// </summary>
    partial void AfterUpdateFrom(System.Numerics.Vector2 source, ref int changeCount);

    /// <summary>
    /// Updates properties from the source object to this object and returns the number of changes.
    /// </summary>
    public new int UpdateFrom(System.Numerics.Vector2 source)
    {
        if (ReferenceEquals(null, source)) throw new ArgumentNullException();
        if (ReferenceEquals(this, source)) return 0;
        var changeCount = 0;
        BeforeUpdateFrom(source, ref changeCount);
        var this_X = this.X;
        var source_X = source.X;
        UpdateFromTransform_X(ref source_X);
        if (!this_X.Equals(source_X)) {
            this.X = source_X;
            changeCount++;
        }
        var this_Y = this.Y;
        var source_Y = source.Y;
        UpdateFromTransform_Y(ref source_Y);
        if (!this_Y.Equals(source_Y)) {
            this.Y = source_Y;
            changeCount++;
        }
        AfterUpdateFrom(source, ref changeCount);
        return changeCount;
    }
}
```

The `UpdateFromTransform_` methods are static.  The `BeforeUpdateFrom` and `AfterUpdateFrom`
methods are not static.

The generated code still has the null checks and adds in a `this` check, even when technically a match is
impossible (eg - because the source is a struct).  The reasoning was to opt on the side of safety.

The generated code introduces a change count variable that is only incremented when a property
or field actually differs.  This example is very simple since we are only using value types.
If we were using reference types, then extra null checks are included for each reference type value.
The test project includes several examples.

The `BeforeUpdateFrom` and `AfterUpdateFrom` methods are given the opportunity to update the
change count themselves.  If they are implemented, they should only update the count if a change
is actually made.


## EnableCopyTo

The `[EnableCopyTo]` attribute creates a new `CopyTo` method in the marked object.  
Supported marked types are `class`, `record`, `struct`, and `record struct`.  
Supported target types are `class`, `struct`, and `interface`.  Basically any type that can
implement read/write properties or fields.

```csharp
// MyVector.cs -- your code
namespace Test;

[EnableCopyTo(typeof(System.Numerics.Vector2))]
public partial record MyVector(float X, float Y);


// MyVector.g.cs -- generated code
namespace Test;

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial record MyVector
{
    /// <summary>
    /// Transforms the X value before assigning the value to the target.
    /// </summary>
    static partial void CopyToTransform_X(ref float value);

    /// <summary>
    /// Transforms the Y value before assigning the value to the target.
    /// </summary>
    static partial void CopyToTransform_Y(ref float value);

    /// <summary>
    /// Method to run before the CopyTo method begins copying values.
    /// </summary>
    partial void BeforeCopyTo(ref System.Numerics.Vector2 target);

    /// <summary>
    /// Method to run after the CopyTo method finishes copying values.
    /// </summary>
    partial void AfterCopyTo(ref System.Numerics.Vector2 target);

    /// <summary>
    /// Copies properties from this object to the target object and returns the target object.
    /// </summary>
    public new MyVector CopyTo(ref System.Numerics.Vector2 target)
    {
        if (ReferenceEquals(null, target)) throw new ArgumentNullException();
        BeforeCopyTo(ref target);
        var this_X = this.X;
        CopyToTransform_X(ref this_X);
        target.X = this_X;
        var this_Y = this.Y;
        CopyToTransform_Y(ref this_Y);
        target.Y = this_Y;
        AfterCopyTo(ref target);
        return this;
    }
}
```

The `CopyToTransform_` methods are static.  The `BeforeCopyTo` and `AfterCopyTo` methods
are not static.  You may also notice that the code correctly identifies the target type
as a struct and utilizes the `ref` keyword on the `BeforeCopyTo`, `AfterCopyTo`, and `CopyTo`
method parameters.

This is pretty much the `CopyFrom` code reversed to read values from `this` and write them
to `target`.  The `CopyFrom` method returns `this` to allow for method chaining.  The `CopyTo`
method also returns `this`, again to allow for method chaining.  This may seem counter-intuitive,
but we are running methods against `this`, not `target`, so we don't necessarily want to change
the scope.  I may add a parameter to the attribute to selectively return `target` instead of
`this`.


## EnableUpdateTarget

The `[EnableUpdateTarget]` attribute creates a new `UpdateTarget` method in the marked object.  
Supported marked types are `class`, `record`, `struct`, and `record struct`.  
Supported target types are `class`, `struct`, and `interface`.  Basically any type that can
implement read/write properties or fields.

```csharp
// MyVector.cs -- your code
namespace Test;

[EnableUpdateTarget(typeof(System.Numerics.Vector2))]
public partial record MyVector(float X, float Y);

// MyVector.g.cs -- generated code
namespace Test;

#nullable enable
#pragma warning disable CS0109  // the member does not hide an inherited member.

partial record MyVector
{
    /// <summary>
    /// Transforms the X value before assigning the value to the target.
    /// </summary>
    static partial void UpdateTargetTransform_X(ref float value);

    /// <summary>
    /// Transforms the Y value before assigning the value to the target.
    /// </summary>
    static partial void UpdateTargetTransform_Y(ref float value);

    /// <summary>
    /// Method to run before the UpdateTarget method begins copying values.
    /// </summary>
    partial void BeforeUpdateTarget(ref System.Numerics.Vector2 target, ref int changeCount);

    /// <summary>
    /// Method to run after the UpdateTarget method finishes copying values.
    /// </summary>
    partial void AfterUpdateTarget(ref System.Numerics.Vector2 target, ref int changeCount);

    /// <summary>
    /// Updates properties in the target object from this object and returns the number of changes.
    /// </summary>
    public new int UpdateTarget(ref System.Numerics.Vector2 target)
    {
        if (ReferenceEquals(null, target)) throw new ArgumentNullException();
        if (ReferenceEquals(this, target)) return 0;
        var changeCount = 0;
        BeforeUpdateTarget(ref target, ref changeCount);
        var target_X = target.X;
        var this_X = this.X;
        UpdateTargetTransform_X(ref this_X);
        if (!target_X.Equals(this_X)) {
            target.X = this_X;
            changeCount++;
        }
        var target_Y = target.Y;
        var this_Y = this.Y;
        UpdateTargetTransform_Y(ref this_Y);
        if (!target_Y.Equals(this_Y)) {
            target.Y = this_Y;
            changeCount++;
        }
        AfterUpdateTarget(ref target, ref changeCount);
        return changeCount;
    }
}
```

The `UpdateTargetTransform_` methods are static.  The `BeforeUpdateTarget` and `AfterUpdateTarget`
methods are not static.  You may also notice that the code correctly identifies the target type
as a struct and utilizes the `ref` keyword on the `BeforeUpdateTarget`, `AfterUpdateTarget`, and 
`UpdateTarget` method parameters.

This is pretty much the `UpdateFrom` code reversed to read values from `this` and write them
to `target`.  The change count is still tracked, and the `BeforeUpdateTarget` and `AfterUpdateTarget`
methods still have an opportunity to update the count.  The change count is still returned from
the method upon completion.



## General Notes

* The attributes can be applied multiple times to your class, and each instance will 
generate overloads taking the type you specified in the attribute.
* The `[SkipOnCopy]` attribute can be applied to any property or field to have that property
or field ignored in all generated code.
There are instances where this could lead to broken code, so use it wisely.
* Review the test cases included with the test project to see many more examples.


## License

This software is licensed under the [MIT license](LICENSE).