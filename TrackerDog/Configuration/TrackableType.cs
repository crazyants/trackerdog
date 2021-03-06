﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace TrackerDog.Configuration
{
    /// <summary>
    /// Represents the default implementation to a fluent trackable type configuration.
    /// </summary>
    internal class TrackableType : ITrackableType, IConfigurableTrackableType
    {
        private readonly ISet<PropertyInfo> _includedProperties = new HashSet<PropertyInfo>(new PropertyInfoEqualityComparer());
        private readonly Type _type;
        private readonly Lazy<IImmutableSet<IObjectPropertyInfo>> _objectPaths;

        public TrackableType(IObjectChangeTrackingConfiguration configuration, Type type)
        {
            //Contract.Requires(type != null, "Give type reference must be non-null");
            Contract.Assert((type.IsClass && !type.IsSealed) || type.IsInterface, $"Given type '{type.AssemblyQualifiedName}' must be either a non-sealed class or an interface");

            _type = type;
            _objectPaths = new Lazy<IImmutableSet<IObjectPropertyInfo>>
            (
                () => Type.BuildAllPropertyPaths(p => configuration.CanTrackType(p.DeclaringType))
                            .Cast<IObjectPropertyInfo>()
                            .ToImmutableHashSet()
            );
        }

        public Type Type => _type;
        public IImmutableSet<IObjectPropertyInfo> ObjectPaths => _objectPaths.Value;
        public IImmutableSet<PropertyInfo> IncludedProperties => _includedProperties.ToImmutableHashSet(new PropertyInfoEqualityComparer());

        public IConfigurableTrackableType IncludeProperty(PropertyInfo property)
        {
            Contract.Requires(property.GetMethod != null && property.SetMethod != null, "Selected property must own a getter and a setter");
            //Contract.Requires(property.DeclaringType == typeof(T), $"Property '{property.DeclaringType.FullName}.{property.Name}' must be declared on the type being configured as trackable. If the property to include is declared on a base type, the whole base type must be also configured as trackable and the so-called property should be included on the particular base type.");
            Contract.Assert(_includedProperties.Add(property), "Property must be included once");

            return this;
        }

        public IConfigurableTrackableType IncludeProperties(params PropertyInfo[] properties)
        {
            return IncludeProperties((IEnumerable<PropertyInfo>)properties);
        }

        public IConfigurableTrackableType IncludeProperties(IEnumerable<PropertyInfo> properties)
        {
            foreach (PropertyInfo property in properties)
                IncludeProperty(property);

            return this;
        }
    }
}