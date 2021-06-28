using System;
using System.Reflection;
using System.Text;

namespace DrfLikePaginations
{
    public static class Base64
    {
        public static string Encode(string value)
        {
            byte[] valueInBytes = Encoding.ASCII.GetBytes(value);
            return Convert.ToBase64String(valueInBytes);
        }

        public static string Decode(string value)
        {
            byte[] encodedInBytes = Convert.FromBase64String(value);
            return Encoding.ASCII.GetString(encodedInBytes);
        }
    }

    public static class Reflections
    {
        public static PropertyInfo RetrievePropertyInfoFromSourceGivenItsName(Type type, string propertyName)
        {
            var flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;
            var property = type?.GetProperty(propertyName, flags);

            if (property is null)
            {
                var message = $"The type {type?.Name} does not have property {propertyName}";
                throw new PropertyMustBePresentException(message);
            }

            return property;
        }

        public static string RetrieveValueAsString(object source, Type type, string propertyName)
        {
            var property = RetrievePropertyInfoFromSourceGivenItsName(type, propertyName);
            var value = property.GetValue(source, null);

            if (value is null)
            {
                var message = $"There is no value in {property.Name}. Are you sure that it should have one?";
                throw new PropertyValueMustBePresentException(message);
            }

            return value.ToString()!;
        }
    }
}
