#if UNITY_EDITOR || UNITY_EDITOR_BETA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using migrationtool.models;
using migrationtool.utility;
using UnityEngine;

namespace migrationtool.controllers
{
    /// <summary>
    /// Generate all the fields on a class
    /// </summary>
    public class FieldGenerationController
    {
        private static Constants constants = Constants.Instance;
        private static KeyValuePair<string, Type>[] cachedFullNameTypeList;

        /// <summary>
        /// Gets all the fields on a class
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static FieldModel[] GenerateFields(string name)
        {
            Type type = GetTypeByFullName(name);
            if (type == null)
            {
                Debug.LogError("Could not find class of name to search for members: " + name);
                return null;
            }

            return GenerateFields(type, 0);
        }


        /// <summary>
        /// Gets all the fields on a class
        /// </summary>
        /// <param name="type"></param>
        /// <param name="iteration">Times it has ran, used to recursively get the children</param>
        /// <returns></returns>
        public static FieldModel[] GenerateFields(Type type, int iteration)
        {
            iteration++;
            
            if (type.FullName != null && type.FullName.Contains('['))
            {
                Match match = constants.IsListOrArrayRegex.Match(type.FullName);
                if (match.Success)
                {
                    string matchedValue = match.Value;
                    type = GetTypeByFullName(matchedValue);
                    if (type == null)
                    {
                        Debug.LogWarning("Type of list or array could not be found : " + match.Value);
                        return null;
                    }
                }
                else
                {
                    throw new NotImplementedException("Could not parse the type from a list. This should never happen, please contact make an issue if it does. Issues can be made at https://github.com/WouterVanmulken/Unity-Migration-Tool/issues");
                }
            }

            FieldInfo[] publicFields =
                type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                               BindingFlags.FlattenHierarchy);
            FieldInfo[] privateSerializedFields = type
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                           BindingFlags.FlattenHierarchy)
                .Where(info => Attribute.IsDefined(info, typeof(SerializeField))).ToArray();

            List<FieldModel> values = new List<FieldModel>(publicFields.Length + privateSerializedFields.Length);

            AddMembersToFieldModels(type,ref values, publicFields, iteration);
            AddMembersToFieldModels(type,ref values, privateSerializedFields, iteration);

            return values.ToArray();
        }

        /// <summary>
        /// Helper method to get the type of a class by the FullName with a cache
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        private static Type GetTypeByFullName(string fullName)
        {
            if (cachedFullNameTypeList == null)
            {
                cachedFullNameTypeList = TypeUtility.GetAllTypesInAssembliesByFullName();
            }  

            KeyValuePair<string, Type> pair = cachedFullNameTypeList.FirstOrDefault(type => type.Key == fullName);

            // if not null
            if (!pair.Equals(default(KeyValuePair<string, Type>)))
            {
                return pair.Value;
            }

            return null;
        }

        /// <summary>
        /// Transform the FieldInfo to a list of fieldModels and add them to the values list
        /// Check if it's a array or list and transform the name 
        /// </summary>
        /// <param name="fieldToAddTo">List in which to add the field model</param>
        /// <param name="members">members to be transformed to the list</param>
        /// <param name="iteration"></param>
        private static void AddMembersToFieldModels(Type parent, ref List<FieldModel> fieldToAddTo, FieldInfo[] members, int iteration)
        {
            for (var i = 0; i < members.Length; i++)
            {
                FieldInfo member = members[i];
                Type currentType = member.FieldType;

                bool isIterable = false;

                if (currentType.IsArray)
                {
                    isIterable = true;
                    currentType = currentType.GetElementType();
                }

                if (currentType.IsGenericList())
                {
                    isIterable = true;
                    currentType = currentType.GetGenericArguments()[0];
                }

                fieldToAddTo.Add(new FieldModel(parent,member.Name, currentType, isIterable, iteration));

                
            }
        }
    }
}
#endif