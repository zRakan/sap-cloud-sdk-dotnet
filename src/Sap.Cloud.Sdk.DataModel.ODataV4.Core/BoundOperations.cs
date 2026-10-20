#pragma warning disable CA1711 // Type names mirror the Java API (Collection suffix).

using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>An operation (function or action) that is bound to an entity or an entity collection.</summary>
/// <typeparam name="TBinding">The entity type the operation is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// Port of <c>BoundOperation</c>. Java passes the binding and result classes as constructor arguments; here the type
/// arguments carry them (<see cref="BindingType"/>, <see cref="ReturnType"/>).
/// </remarks>
public interface IBoundOperation<TBinding, TResult>
{
    /// <summary>The qualified name of the operation, for example <c>Trippin.GetFavoriteAirline</c>.</summary>
    string QualifiedName { get; }

    /// <summary>The entity type the operation is bound to.</summary>
    Type BindingType { get; }

    /// <summary>The result type.</summary>
    Type ReturnType { get; }
}

/// <summary>A bound function: it has no side effects and is invoked with <c>GET</c>.</summary>
/// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IBoundFunction<TBinding, TResult> : IBoundOperation<TBinding, TResult>
{
    /// <summary>The function parameters.</summary>
    ODataFunctionParameters Parameters { get; }
}

/// <summary>A bound function whose result can be navigated further (it returns an entity or entities).</summary>
/// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IComposableBoundFunction<TBinding, TResult> : IBoundFunction<TBinding, TResult>
{
}

/// <summary>A bound action: it may have side effects and is invoked with <c>POST</c>.</summary>
/// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IBoundAction<TBinding, TResult> : IBoundOperation<TBinding, TResult>
{
    /// <summary>The action parameters.</summary>
    IReadOnlyDictionary<string, object?> Parameters { get; }
}

/// <summary>Shared implementation of bound operations.</summary>
/// <typeparam name="TBinding">The entity type the operation is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of the package-private <c>AbstractBoundOperation</c>.</remarks>
public abstract class BoundOperationBase<TBinding, TResult> : IBoundOperation<TBinding, TResult>
{
    private protected BoundOperationBase(string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);
        QualifiedName = qualifiedName;
    }

    /// <inheritdoc />
    public string QualifiedName { get; }

    /// <inheritdoc />
    public Type BindingType => typeof(TBinding);

    /// <inheritdoc />
    public Type ReturnType => typeof(TResult);
}

/// <summary>Shared implementation of bound functions.</summary>
/// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public abstract class BoundFunctionBase<TBinding, TResult> : BoundOperationBase<TBinding, TResult>, IBoundFunction<TBinding, TResult>
{
    private protected BoundFunctionBase(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters)
        : base(qualifiedName)
    {
        Parameters = ODataFunctionParameters.Of(parameters ?? [], ODataProtocol.V4);
    }

    /// <inheritdoc />
    public ODataFunctionParameters Parameters { get; }
}

/// <summary>Shared implementation of bound actions.</summary>
/// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public abstract class BoundActionBase<TBinding, TResult> : BoundOperationBase<TBinding, TResult>, IBoundAction<TBinding, TResult>
{
    private protected BoundActionBase(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters)
        : base(qualifiedName)
    {
        Parameters = new Dictionary<string, object?>(parameters ?? []);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

/// <summary>The result type of an action that returns nothing (Java: <c>Void</c>).</summary>
public sealed class VdmVoid
{
    private VdmVoid()
    {
    }
}

/// <summary>The kinds of bound functions, by cardinality of binding and result and by type of result.</summary>
/// <remarks>
/// Port of the nested classes of Java's <c>BoundFunction</c> interface. The four base classes are
/// <c>SingleToSingle</c>, <c>SingleToCollection</c>, <c>CollectionToSingle</c> and <c>CollectionToCollection</c>; each
/// has a <c>Primitive</c>, <c>Complex</c> and <c>Entity</c> flavor and the entity flavor a <c>Composable</c> one
/// (named <c>...EntityComposable</c> because C# nested types of generic types inherit the type parameters).
/// </remarks>
public static class BoundFunction
{
    /// <summary>Bound to one entity, returns one value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToSingle<TBinding, TResult> : BoundFunctionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSingle{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSingle(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns one value; the result is a primitive value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToSinglePrimitive<TBinding, TResult> : SingleToSingle<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSinglePrimitive{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSinglePrimitive(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns one value; the result is a complex value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToSingleComplex<TBinding, TResult> : SingleToSingle<TBinding, TResult>
        where TResult : VdmComplex
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSingleComplex{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSingleComplex(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns one value; the result is an entity.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToSingleEntity<TBinding, TResult> : SingleToSingle<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSingleEntity{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSingleEntity(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns one value; the result is an entity and can be navigated further.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class SingleToSingleEntityComposable<TBinding, TResult> : SingleToSingleEntity<TBinding, TResult>, IComposableBoundFunction<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSingleEntityComposable{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSingleEntityComposable(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToCollection<TBinding, TResult> : BoundFunctionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollection{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollection(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection; the result is a primitive value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToCollectionPrimitive<TBinding, TResult> : SingleToCollection<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollectionPrimitive{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollectionPrimitive(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection; the result is a complex value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToCollectionComplex<TBinding, TResult> : SingleToCollection<TBinding, TResult>
        where TResult : VdmComplex
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollectionComplex{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollectionComplex(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection; the result is an entity.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class SingleToCollectionEntity<TBinding, TResult> : SingleToCollection<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollectionEntity{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollectionEntity(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection; the result is an entity and can be navigated further.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class SingleToCollectionEntityComposable<TBinding, TResult> : SingleToCollectionEntity<TBinding, TResult>, IComposableBoundFunction<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollectionEntityComposable{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollectionEntityComposable(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToSingle<TBinding, TResult> : BoundFunctionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSingle{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSingle(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value; the result is a primitive value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToSinglePrimitive<TBinding, TResult> : CollectionToSingle<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSinglePrimitive{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSinglePrimitive(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value; the result is a complex value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToSingleComplex<TBinding, TResult> : CollectionToSingle<TBinding, TResult>
        where TResult : VdmComplex
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSingleComplex{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSingleComplex(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value; the result is an entity.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToSingleEntity<TBinding, TResult> : CollectionToSingle<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSingleEntity{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSingleEntity(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value; the result is an entity and can be navigated further.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class CollectionToSingleEntityComposable<TBinding, TResult> : CollectionToSingleEntity<TBinding, TResult>, IComposableBoundFunction<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSingleEntityComposable{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSingleEntityComposable(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToCollection<TBinding, TResult> : BoundFunctionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollection{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollection(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection; the result is a primitive value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToCollectionPrimitive<TBinding, TResult> : CollectionToCollection<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollectionPrimitive{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollectionPrimitive(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection; the result is a complex value.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToCollectionComplex<TBinding, TResult> : CollectionToCollection<TBinding, TResult>
        where TResult : VdmComplex
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollectionComplex{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollectionComplex(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection; the result is an entity.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public class CollectionToCollectionEntity<TBinding, TResult> : CollectionToCollection<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollectionEntity{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollectionEntity(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection; the result is an entity and can be navigated further.</summary>
    /// <typeparam name="TBinding">The entity type the function is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class CollectionToCollectionEntityComposable<TBinding, TResult> : CollectionToCollectionEntity<TBinding, TResult>, IComposableBoundFunction<TBinding, TResult>
        where TResult : VdmEntity
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollectionEntityComposable{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the function.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollectionEntityComposable(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }
}

/// <summary>The kinds of bound actions, by cardinality of binding and result.</summary>
/// <remarks>Port of the nested classes of Java's <c>BoundAction</c> interface.</remarks>
public static class BoundAction
{
    /// <summary>Bound to one entity, returns one value.</summary>
    /// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class SingleToSingle<TBinding, TResult> : BoundActionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToSingle{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the action.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToSingle(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to one entity, returns a collection.</summary>
    /// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class SingleToCollection<TBinding, TResult> : BoundActionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="SingleToCollection{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the action.</param>
        /// <param name="parameters">The parameters.</param>
        public SingleToCollection(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns one value.</summary>
    /// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class CollectionToSingle<TBinding, TResult> : BoundActionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToSingle{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the action.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToSingle(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }

    /// <summary>Bound to an entity collection, returns a collection.</summary>
    /// <typeparam name="TBinding">The entity type the action is bound to.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    public sealed class CollectionToCollection<TBinding, TResult> : BoundActionBase<TBinding, TResult>
    {
        /// <summary>Initializes a new instance of the <see cref="CollectionToCollection{TBinding, TResult}"/> class.</summary>
        /// <param name="qualifiedName">The qualified name of the action.</param>
        /// <param name="parameters">The parameters.</param>
        public CollectionToCollection(string qualifiedName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
            : base(qualifiedName, parameters)
        {
        }
    }
}
