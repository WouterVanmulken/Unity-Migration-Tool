using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using importerexporter.models;
using UnityEngine;

namespace importerexporter.utility
{
    /// <summary>
    /// Generate all the fields on a class
    /// </summary>
    public static class FieldDataGenerationUtility
    {
        private static Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        /// <summary>
        /// Gets all the fields on a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static FieldModel[] GenerateFieldData(string name)
        {
            Type type = assemblies.SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.FullName == name);
            if (type == null)
            {
                Debug.LogError("Could not find class of name to search for members: " + name);
                return null;
            }

            return GenerateFieldData(type, 0);
        }

        /// <summary>
        /// Gets all the fields on a class
        /// </summary>
        /// <param name="type"></param>
        /// <param name="iteration">Times it has ran, used to recursively get the children</param>
        /// <returns></returns>
        public static FieldModel[] GenerateFieldData(Type type, int iteration)
        {
            iteration++;
            List<FieldModel> values = new List<FieldModel>();

            FieldInfo[] publicFields =
                type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                               BindingFlags.FlattenHierarchy);
            FieldInfo[] privateSerializedFields = type
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                           BindingFlags.FlattenHierarchy)
                .Where(info => Attribute.IsDefined(info, typeof(SerializeField))).ToArray();

            List<FieldInfo> members = new List<FieldInfo>();
            members.AddRange(publicFields);
            members.AddRange(privateSerializedFields);

            for (var i = 0; i < members.Count; i++)
            {
                FieldInfo member = members[i];
                values.Add(new FieldModel(member.Name, member.FieldType, iteration));
            }

            return values.ToArray();
        }
    }
}