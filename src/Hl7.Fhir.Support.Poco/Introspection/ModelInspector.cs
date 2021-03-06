/* 
 * Copyright (c) 2014, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/master/LICENSE
 */

using Hl7.Fhir.Specification;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hl7.Fhir.Introspection
{
    /// <summary>
    /// A cache of FHIR type mappings found on .NET classes.
    /// </summary>
    /// <remarks>Holds the mappings for a specific version of FHIR, even though some POCO's can 
    /// specify the definition of multiple versions of FHIR. 
    /// </remarks>
    public class ModelInspector : IStructureDefinitionSummaryProvider
    {

        public ModelInspector(FhirRelease fhirVersion)
        {
            _fhirVersion = fhirVersion;
        }

        private readonly FhirRelease _fhirVersion;

        // Index for easy lookup of datatypes, key is upper typenanme
        private readonly ConcurrentDictionary<string, ClassMapping> _classMappingsByName =
            new ConcurrentDictionary<string, ClassMapping>();

        // Primary index of classmappings, key is Type
        private readonly ConcurrentDictionary<Type, ClassMapping> _classMappingsByType =
             new ConcurrentDictionary<Type, ClassMapping>();

        public IReadOnlyList<IStructureDefinitionSummary> Import(Assembly assembly)
        {
            if (assembly == null) throw Error.ArgumentNull(nameof(assembly));

#if NET40
            IEnumerable<Type> exportedTypes = assembly.GetExportedTypes();
#else
            IEnumerable<Type> exportedTypes = assembly.ExportedTypes;
#endif

            return exportedTypes.Select(t => ImportType(t)).ToList();
        }

        public IStructureDefinitionSummary Provide(string canonical)
        {
            var isLocalType = !canonical.Contains("/");

            if (!isLocalType)
            {
                // So, we have received a canonical url, not being a relative path
                // (know resource/datatype), we -for now- only know how to get a ClassMapping
                // for this, if it's a built-in T4 generated POCO, so there's no way
                // to find a mapping for this.
                return null;
            }

            return FindClassMapping(canonical);
        }


        public ClassMapping ImportType(Type type)
        {
            if (_classMappingsByType.TryGetValue(type, out var mapping))
                return mapping;     // no need to import the same type twice

            // Don't import types that aren't marked with [FhirType]
            if (ClassMapping.GetAttribute<FhirTypeAttribute>(type.GetTypeInfo(), _fhirVersion) == null) return null;

            // When explicitly importing a (newer?) class mapping for the same
            // model type name, overwrite the old entry.
            return getOrAddClassMappingForTypeInternal(type, overwrite: true);
        }

        private ClassMapping createMapping(Type type, FhirRelease version)
        {
            if (!ClassMapping.TryCreate(type, out var mapping, version))
            {
                Message.Info("Skipped type {0} while doing inspection: not recognized as representing a FHIR type", type.Name);
                return null;
            }
            else
            {
                Message.Info("Created Class mapping for newly encountered type {0} (FHIR type {1})", type.Name, mapping.Name);
                return mapping;
            }
        }

        private ClassMapping getOrAddClassMappingForTypeInternal(Type type, bool overwrite)
        {
            var typeMapping = _classMappingsByType.GetOrAdd(type, tp => createMapping(tp, _fhirVersion));

            if (typeMapping == null) return null;

            // Whether we are pre-empted here or not, resultMapping will always be "the" single instance
            // with a mapping for this type, shared across all threads. Now we are going to add an entry by
            // name for this mapping.

            var key = typeMapping.Name;
            // Add this mapping by name of the mapping, overriding any entry already present
            if (overwrite)
            {
                _ = _classMappingsByName
                            .AddOrUpdate(key, typeMapping, (_, __) => typeMapping);
            }
            else
            {
                var nameMapping = _classMappingsByName.GetOrAdd(key, typeMapping);

                if (!object.ReferenceEquals(typeMapping, nameMapping))
                {
                    // ouch, there was already a mapping under this name, that does not correspond to the instance
                    // for our type mapping -> there must be multiple types having a mapping for the same model type.
                    throw new ArgumentException($"Type '{type.Name}' has a mapping for model type '{typeMapping.Name}', " +
                        $"but type '{nameMapping.NativeType.Name}' was already registered for that model type.");
                }
            }

            return typeMapping;
        }

        public ClassMapping FindClassMapping(string name) =>
            _classMappingsByName.TryGetValue(name, out var entry) ? entry : null;

        public ClassMapping FindClassMapping(Type t) =>
            _classMappingsByType.TryGetValue(t, out var entry) ? entry : null;
    }
}
