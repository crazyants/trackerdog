﻿namespace TrackerDog
{
    using Castle.DynamicProxy;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using TrackerDog.Configuration;

    /// <summary>
    /// Represents a set of object change-tracking related operations that work as façades to simplify the work
    /// with change tracking.
    /// </summary>
    public static class ObjectChangeTrackingExtensions
    {
        public static IImmutableSet<IObjectPropertyInfo> BuildAllPropertyPaths(this Type someType, Func<PropertyInfo, bool> filter = null)
            => BuildAllPropertyPathsInternal(someType, filter: filter);

        private static IImmutableSet<IObjectPropertyInfo> BuildAllPropertyPathsInternal(this Type someType, ISet<ObjectPropertyInfo> paths = null, PropertyInfo ownerProperty = null, ObjectPropertyInfo currentPropertyInfo = null, Func<PropertyInfo, bool> filter = null)
        {
            Contract.Requires(someType != null, "Given type must be a non-null reference");
            Contract.Ensures(Contract.Result<IImmutableSet<IObjectPropertyInfo>>() != null);

            paths = paths ?? new HashSet<ObjectPropertyInfo>();

            foreach (PropertyInfo property in
                someType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (filter == null || filter(property))
                {
                    if (currentPropertyInfo == null || property.DeclaringType.IsAssignableFrom(currentPropertyInfo.PathParts[0].DeclaringType))
                        currentPropertyInfo = new ObjectPropertyInfo();
                    else
                        currentPropertyInfo = currentPropertyInfo.Clone();

                    if (currentPropertyInfo.PathParts.Count > 1 && property.DeclaringType.IsAssignableFrom(currentPropertyInfo.PathParts.Last().DeclaringType))
                    {
                        int lastItemIndex = currentPropertyInfo.PathParts.Count - 1;
                        currentPropertyInfo.PathParts.RemoveAt(lastItemIndex);
                        currentPropertyInfo.PathParts.Add(property);
                    }
                    else
                        currentPropertyInfo.PathParts.Add(property);

                    paths.Add(currentPropertyInfo);

                    BuildAllPropertyPathsInternal(property.PropertyType, paths, property, currentPropertyInfo, filter);
                }
            }

            return paths.Cast<IObjectPropertyInfo>().ToImmutableHashSet();
        }

        /// <summary>
        /// Gets a configured trackable type by type, or returns null if it's not already configured.
        /// </summary>
        /// <param name="someType">The whole type to get its tracking configuration</param>
        /// <returns>The configured trackable type by type, or returns null if it's not already configured</returns>
        public static ITrackableType GetTrackableType(this Type someType)
            => TrackerDogConfiguration.GetTrackableType(someType);

        /// <summary>
        /// Determines if given type can be tracked as collection
        /// </summary>
        /// <param name="some">The whole type to check</param>
        /// <returns><literal>true</literal> if can be tracked as collection, <literal>false</literal> if it can't be tracked as collection</returns>
        internal static bool CanBeTrackedAsCollection(this Type some)
        {
            Contract.Requires(some != null, "Given type must be a non-null reference");

            return TrackerDogConfiguration.Collections.CanTrack(some);
        }

        /// <summary>
        /// Determines if given property can be tracked as collection
        /// </summary>
        /// <param name="some">The whole property to check</param>
        /// <returns><literal>true</literal> if can be tracked as collection, <literal>false</literal> if it can't be tracked as collection</returns>
        internal static bool CanBeTrackedAsCollection(this PropertyInfo some)
        {
            Contract.Requires(some != null, "Given property must be a non-null reference");

            return some.PropertyType.CanBeTrackedAsCollection();
        }

        /// <summary>
        /// Determines if given object type can be tracked as collection
        /// </summary>
        /// <param name="some">The whole object to check its type</param>
        /// <returns><literal>true</literal> if can be tracked as collection, <literal>false</literal> if it can't be tracked as collection</returns>
        internal static bool CanBeTrackedAsCollection(this object some)
        {
            Contract.Requires(some != null, "Given object must be a non-null reference");

            return some.GetType().CanBeTrackedAsCollection();
        }

        /// <summary>
        /// Turns some object into a trackable object.
        /// </summary>
        /// <typeparam name="TObject">The type of the object to track changes</typeparam>
        /// <param name="some">The object to track its changes</param>
        /// <returns>A proxy of the given object to track its changes</returns>
        public static TObject AsTrackable<TObject>(this TObject some)
            where TObject : class
        {
            Contract.Requires(some != null, "Reference must not be null to turn an object into a trackable one");

            return TrackableObjectFactory.Create(some: some);
        }

        /// <summary>
        /// Turns some object into a trackable object.
        /// </summary>
        /// <typeparam name="TObject">The type of the object to track changes</typeparam>
        /// <param name="some">The object to track its changes</param>
        /// <param name="propertyToSet">The property to which the proxy must be set to</param>
        /// <returns>A proxy of the given object to track its changes</returns>
        internal static TObject AsTrackable<TObject>(this TObject some, PropertyInfo propertyToSet)
            where TObject : class
        {
            Contract.Requires(some != null, "Reference must not be null to turn an object into a trackable one");

            return TrackableObjectFactory.Create(some: some, propertyToSet: propertyToSet);
        }

        /// <summary>
        /// Turns an object held by a property into a change-trackable one.
        /// </summary>
        /// <param name="property">The whole property</param>
        /// <param name="parentObject">The object owning the property</param>
        internal static void AsTrackableCollection(this PropertyInfo property, IChangeTrackableObject parentObject)
        {
            Contract.Requires(property != null, "Cannot turn the object held by the property because the given property is null");
            Contract.Requires(parentObject != null, "A non-null reference to the object owning given property is mandatory");

            if (property.IsEnumerable() && property.CanBeTrackedAsCollection())
                property.SetValue(parentObject, TrackableObjectFactory.CreateForCollection(property.GetValue(parentObject), parentObject, property));
        }

        /// <summary>
        /// Determines if a given object is a change-trackable object already
        /// </summary>
        /// <param name="some">The object to check</param>
        /// <returns><literal>true</literal> if it's change-trackable, <literal>false</literal> if it's not change-trackable</returns>
        public static bool IsTrackable(this object some)
        {
            return some is IChangeTrackableObject || some is IChangeTrackableCollection;
        }

        /// <summary>
        /// Gets if non-proxied object type is already change-trackable
        /// </summary>
        /// <param name="some">The whole object to check</param>
        /// <returns><literal>true</literal> if it's trackable, <literal>false</literal> if it's not trackable</returns>
        public static Type GetActualTypeIfTrackable(this object some)
        {
            Contract.Requires(some != null, "Reference must not be null to get actual object type");
            Contract.Ensures(Contract.Result<Type>() != null);

            return GetActualTypeIfTrackable(some.GetType());
        }
        /// <summary>
        /// Gets if non-proxied type is already change-trackable
        /// </summary>
        /// <param name="some">The whole type to check</param>
        /// <returns><literal>true</literal> if it's trackable, <literal>false</literal> if it's not trackable</returns>
        public static Type GetActualTypeIfTrackable(this Type some)
        {
            Contract.Requires(some != null, "Reference must not be null to get actual object type");
            Contract.Ensures(Contract.Result<Type>() != null);

            if (some.IsTrackable())
                return some.BaseType;
            else
                return some;
        }

        /// <summary>
        /// Determines if a given type is already change-trackable
        /// </summary>
        /// <param name="some">The type to check</param>
        /// <returns><literal>true</literal> if it's change-trackable, <literal>false</literal> if it's not change-trackable</returns>
        public static bool IsTrackable(this Type some)
        {
            Contract.Requires(some != null, "Reference must not be null to check if a type is trackable");

            return typeof(IChangeTrackableObject).IsAssignableFrom(some);
        }

        /// <summary>
        /// Gets current tracked object change tracker.
        /// </summary>
        /// <param name="some">The change-tracked object</param>
        /// <returns>The change tracker</returns>
        public static IObjectChangeTracker GetChangeTracker(this object some)
        {
            Contract.Requires(some != null, "Reference must not be null to get object change tracker");
            Contract.Ensures(Contract.Result<IObjectChangeTracker>() != null);

            IChangeTrackableObject trackableObject = some as IChangeTrackableObject;

            if (trackableObject == null)
            {
                IHasParent withParent = some as IHasParent;

                if (withParent != null)
                    trackableObject = withParent.ParentObject;
            }

            Contract.Assert(trackableObject != null, "An object must be trackable in order to get its change tracker");

            return trackableObject.ChangeTracker;
        }

        /// <summary>
        /// Gets a property change tracking for a given property
        /// </summary>
        /// <typeparam name="TObject">The type of tracked object</typeparam>
        /// <typeparam name="TReturn">The type of the property</typeparam>
        /// <param name="some">The tracked object</param>
        /// <param name="propertySelector">A property selector</param>
        /// <returns>The property tracking</returns>
        public static IDeclaredObjectPropertyChangeTracking GetPropertyTracking<TObject, TReturn>(this TObject some, Expression<Func<TObject, TReturn>> propertySelector)
        {
            Contract.Requires(some != null, "Reference must not be null because the given object is required to get its change tracker and find the desired property tracking");
            Contract.Requires(propertySelector != null, "A property selector is mandatory to get a property tracking");

            return some.GetChangeTracker().GetTrackingByProperty(propertySelector) as IDeclaredObjectPropertyChangeTracking;
        }

        /// <summary>
        /// Gets a property change tracking for a given property
        /// </summary>
        /// <param name="some">The tracked object</param>
        /// <param name="property">A property</param>
        /// <returns>The property tracking</returns>
        public static IDeclaredObjectPropertyChangeTracking GetPropertyTracking(this object some, PropertyInfo property)
        {
            Contract.Requires(some != null, "Reference must not be null because the given object is required to get its change tracker and find the desired property tracking");
            Contract.Requires(property != null, "A property is mandatory to get a property tracking");

            return some.GetChangeTracker().GetTrackingByProperty(property) as IDeclaredObjectPropertyChangeTracking;
        }

        /// <summary>
        /// Gets a property change tracking for a given property
        /// </summary>
        /// <param name="some">The tracked object</param>
        /// <param name="propertyName">A property selector</param>
        /// <returns>The property tracking</returns>
        public static IObjectPropertyChangeTracking GetPropertyTracking(this object some, string propertyName)
        {
            Contract.Requires(some != null, "Reference must not be null because the given object is required to get its change tracker and find the desired property tracking");
            Contract.Requires(!string.IsNullOrEmpty(propertyName), "A property name is mandatory to get a property tracking");

            ObjectChangeTracker tracker = (ObjectChangeTracker)some.GetChangeTracker();

            if (tracker.DynamicPropertyTrackings.ContainsKey(propertyName))
                return tracker.GetDynamicTrackingByProperty(propertyName);
            else
            {
                Dictionary<string, DeclaredObjectPropertyChangeTracking> declaredPropertyTrackings = tracker.PropertyTrackings
                                                                    .ToDictionary(t => t.Key.Name, t => t.Value);

                DeclaredObjectPropertyChangeTracking declaredPropertyTracking;

                if (declaredPropertyTrackings.TryGetValue(propertyName, out declaredPropertyTracking))
                    return declaredPropertyTracking;
                else
                    throw new InvalidOperationException
                    (
                        $"Cannot locate a tracking for the given property name '{propertyName}'. This can be either caused because there is more than a property with the given name or actually the given property is not being tracked for changes"
                    );
            }
        }

        /// <summary>
        /// Accepts all changes made to the change-tracked object and its associations.
        /// </summary>
        /// <param name="some">The change-tracked object</param>
        public static void AcceptChanges(this object some)
        {
            Contract.Requires(some != null, "A non-null reference is required to accept tracked object changes");

            IChangeTrackableObject trackableObject = some as IChangeTrackableObject;

            Contract.Assert(trackableObject != null, "Given object must be trackable to accept its changes");

            trackableObject.ChangeTracker.Complete();
        }

        /// <summary>
        /// Undoes all changes made to the change-tracked object and its associations.
        /// </summary>
        /// <param name="some">The change-tracked object</param>
        public static void UndoChanges(this object some)
        {
            Contract.Requires(some != null, "A non-null reference is required to undo tracked object changes");

            IChangeTrackableObject trackableObject = some as IChangeTrackableObject;

            Contract.Assert(trackableObject != null, "Given object must be trackable to undo its changes");

            trackableObject.ChangeTracker.Discard();
        }

        /// <summary>
        /// Iterates the given enumerable and returns an instance of given target collection type configured implementation
        /// where each item will be also converted to untracked objects.
        /// </summary>
        /// <param name="enumerable">The whole enumerable to untrack</param>
        /// <param name="targetCollectionType">The whole target collection type. This type should be a supported trackable collection type</param>
        /// <returns>A copy of source enumerable turned into an untracked collection</returns>
        public static IEnumerable ToUntrackedEnumerable(this IEnumerable enumerable, Type targetCollectionType)
        {
            Contract.Requires(targetCollectionType != null, "Target collection type cannot be a non-null reference");

            if (enumerable != null)
            {
                Type collectionItemType = enumerable.GetCollectionItemType();

                List<Type> collectionTypeArguments = new List<Type>();

                if (collectionItemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    collectionTypeArguments.AddRange(collectionItemType.GenericTypeArguments);
                else
                    collectionTypeArguments.Add(enumerable.GetCollectionItemType());

                IEnumerable enumerableCopy =
                    (IEnumerable)TrackerDogConfiguration.Collections.GetImplementation(targetCollectionType)
                        .Value.Type.CreateInstanceWithGenericArgs(null, collectionTypeArguments.ToArray());

                Type collectionInterface = enumerableCopy.GetType()
                                                .GetInterfaces()
                                                .Single(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>));

                MethodInfo addMethod = collectionInterface.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);

                Contract.Assert(addMethod != null);

                foreach (object item in enumerable)
                {
                    addMethod.Invoke(enumerableCopy, new[] { item.ToUntracked() });
                }

                return enumerableCopy;
            }
            else return null;
        }

        /// <summary>
        /// Turns given object and all associates to untrackable objects (i.e. POCO objects).
        /// </summary>
        /// <typeparam name="TObject">The type of the whole object to untrack</typeparam>
        /// <param name="some">The whole object to untrack</param>
        /// <returns>The untracked version of given object</returns>
        public static TObject ToUntracked<TObject>(this TObject some)
            where TObject : class
        {
            Contract.Ensures(!(Contract.Result<TObject>() is IProxyTargetAccessor), "To convert a tracked object to untracked one the whole tracked object must be created from a pre-existing object");

            if (some == null)
                return some;

            IChangeTrackableObject trackable = some as IChangeTrackableObject;

            if (trackable != null)
            {
                IProxyTargetAccessor proxyTargetAccessor = (IProxyTargetAccessor)trackable;
                TObject unwrapped = (TObject)proxyTargetAccessor.DynProxyGetTarget();

                ObjectChangeTracker changeTracker = (ObjectChangeTracker)trackable.GetChangeTracker();

                if (trackable.CollectionProperties.Count > 0)
                {
                    foreach (PropertyInfo property in trackable.CollectionProperties)
                    {
                        if (property.CanWrite)
                        {
                            PropertyInfo unwrappedProperty = property.GetBaseProperty();
                            object propertyValue = property.GetValue(some);

                            IProxyTargetAccessor propertyValueProxyAccessor = propertyValue as IProxyTargetAccessor;

                            if (propertyValueProxyAccessor != null)
                            {
                                IEnumerable enumerablePropertyValue = propertyValue as IEnumerable;

                                if (enumerablePropertyValue != null)
                                {
                                    unwrappedProperty.SetValue
                                    (
                                        unwrapped,
                                        enumerablePropertyValue.ToUntrackedEnumerable(unwrappedProperty.PropertyType)
                                    );
                                }
                            }
                        }
                    }
                }

                foreach (PropertyInfo declaredProperty in changeTracker.PropertyTrackings.Select(t => t.Key.GetBaseProperty()))
                    if (declaredProperty.DeclaringType == unwrapped.GetType() && declaredProperty.CanWrite)
                        declaredProperty.SetValue
                        (
                            unwrapped, declaredProperty.GetValue(unwrapped).ToUntracked()
                        );

                foreach (KeyValuePair<string, ObjectPropertyChangeTracking> dynamicProperty in changeTracker.DynamicPropertyTrackings)
                {
                    object propertyValueToSet;
                    IChangeTrackableCollection trackableCollection = dynamicProperty.Value.CurrentValue as IChangeTrackableCollection;

                    if (trackableCollection != null)
                    {
                        propertyValueToSet = ((IEnumerable)trackableCollection).ToUntrackedEnumerable(trackableCollection.GetType());
                    }
                    else
                    {
                        propertyValueToSet = dynamicProperty.Value.CurrentValue.ToUntracked();
                    }

                    unwrapped.SetDynamicMember
                    (
                        dynamicProperty.Value.PropertyName,
                        propertyValueToSet
                    );
                }

                return unwrapped;

            }
            else return some;
        }

        /// <summary>
        /// Gets the value of given selected property that had when the change-tracked object started to track its changes.
        /// </summary>
        /// <typeparam name="T">The type of the change-tracked object</typeparam>
        /// <typeparam name="TReturn">The type of the property to gets its unchanged value</typeparam>
        /// <param name="some">The change-tracked object</param>
        /// <param name="propertySelector">The property selector</param>
        /// <returns>The value of the property when it was started to be tracked</returns>
        /// <example>
        /// <code language="c#">
        /// var oldValue = some.OldPropertyValue(o => o.Text);
        /// </code>
        /// </example>
        public static TReturn OldPropertyValue<T, TReturn>(this T some, Expression<Func<T, TReturn>> propertySelector)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to get an object old value property");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return (TReturn)some.GetPropertyTracking(propertySelector).OldValue;
        }

        /// <summary>
        /// Gets the value of given selected property that had when the change-tracked object started to track its changes.
        /// </summary>
        /// <param name="some">The change-tracked object</param>
        /// <param name="propertyName">The property name</param>
        /// <returns>The value of the property when it was started to be tracked</returns>
        public static dynamic OldPropertyValue(this object some, string propertyName)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to get an object old property value");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return some.GetPropertyTracking(propertyName).OldValue;
        }

        /// <summary>
        /// Gets the last value of given selected property.
        /// </summary>
        /// <typeparam name="T">The type of the change-tracked object</typeparam>
        /// <typeparam name="TReturn">The type of the property to gets its last value</typeparam>
        /// <param name="some">The change-tracked object</param>
        /// <param name="propertySelector">The property selector</param>
        /// <returns>The last value of the property</returns>
        /// <example>
        /// <code language="c#">
        /// var currentValue = some.CurrentPropertyValue(o => o.Text);
        /// </code>
        /// </example>
        public static TReturn CurrentPropertyValue<T, TReturn>(this T some, Expression<Func<T, TReturn>> propertySelector)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to get an object current property value");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return (TReturn)some.GetPropertyTracking(propertySelector).CurrentValue;
        }

        /// <summary>
        /// Gets the last value of given selected property.
        /// </summary>
        /// <param name="some">The change-tracked object</param>
        /// <param name="propertyName">The property name</param>
        /// <returns>The last value of the property</returns>
        public static dynamic CurrentPropertyValue(this object some, string propertyName)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to get an object current property value");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return some.GetPropertyTracking(propertyName).CurrentValue;
        }

        /// <summary>
        /// Determines if a given property by selector has changed since its tracking was started.
        /// </summary>
        /// <typeparam name="T">The type of the object owning the whole property</typeparam>
        /// <param name="some">The change-tracked object</param>
        /// <param name="propertySelector">The property selector</param>
        /// <returns><codeInline>true</codeInline> if it has changed, <codeInline>false</codeInline> if it doesn't changed.</returns>
        public static bool PropertyHasChanged<T>(this T some, Expression<Func<T, object>> propertySelector)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to check if a property has changed");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return some.GetPropertyTracking(propertySelector).HasChanged;
        }

        /// <summary>
        /// Determines if a given property by name has changed since its tracking was started.
        /// </summary>
        /// <param name="some">The trackable object</param>
        /// <param name="propertyName">The property selector</param>
        /// <returns><codeInline>true</codeInline> if it has changed, <codeInline>false</codeInline> if it doesn't changed.</returns>
        public static bool PropertyHasChanged(this object some, string propertyName)
        {
            Contract.Requires(some != null, "A non-null reference is mandatory to check if a property has changed");
            Contract.Requires(some is IChangeTrackableObject, "Given object must be trackable");

            return some.GetPropertyTracking(propertyName).HasChanged;
        }
    }
}