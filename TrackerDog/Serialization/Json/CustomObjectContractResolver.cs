﻿using Newtonsoft.Json.Serialization;
using System;
using System.Dynamic;

namespace TrackerDog.Serialization.Json
{
    internal class CustomObjectContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(objectType))
            {
                return CreateObjectContract(objectType);
            }

            return base.CreateContract(objectType);
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);

            contract.DictionaryKeyResolver = propertyName => propertyName;

            return contract;
        }
    }
}