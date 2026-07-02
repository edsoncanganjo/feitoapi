using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace feitoapi.api.Infrastructure;

/// <summary>
/// Recursively validates a model graph using DataAnnotations. Minimal APIs do not
/// deep-validate nested objects/collections out of the box, so we walk them here.
/// </summary>
public static class ModelValidation
{
    public static bool TryValidate(object? model, out List<string> errors)
    {
        errors = new List<string>();
        if (model is null)
        {
            errors.Add("Request body is required.");
            return false;
        }
        Validate(model, prefix: string.Empty, errors, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return errors.Count == 0;
    }

    private static void Validate(object obj, string prefix, List<string> errors, HashSet<object> visited)
    {
        if (!visited.Add(obj)) return; // guard against cycles

        var context = new ValidationContext(obj);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
        foreach (var r in results)
        {
            var member = r.MemberNames.FirstOrDefault();
            var name = string.IsNullOrEmpty(prefix)
                ? member ?? "model"
                : member is null ? prefix : $"{prefix}.{member}";
            errors.Add($"{name}: {r.ErrorMessage}");
        }

        // Recurse into complex properties and collections of complex types.
        foreach (var prop in obj.GetType().GetProperties())
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            var value = prop.GetValue(obj);
            if (value is null) continue;

            var t = prop.PropertyType;
            if (IsSimple(t)) continue;

            var childPrefix = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            if (value is IEnumerable enumerable and not string)
            {
                var i = 0;
                foreach (var element in enumerable)
                {
                    if (element is not null && !IsSimple(element.GetType()))
                        Validate(element, $"{childPrefix}[{i}]", errors, visited);
                    i++;
                }
            }
            else
            {
                Validate(value, childPrefix, errors, visited);
            }
        }
    }

    private static bool IsSimple(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive
            || u.IsEnum
            || u == typeof(string)
            || u == typeof(decimal)
            || u == typeof(DateTime)
            || u == typeof(DateOnly)
            || u == typeof(TimeOnly)
            || u == typeof(Guid);
    }
}
