using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.Models.Design;
using Nitrox.Launcher.Models.Services;
using Nitrox.Launcher.ViewModels.Abstract;

namespace Nitrox.Launcher.ViewModels;

internal partial class ObjectPropertyEditorViewModel(DialogService dialogService) : ModalViewModelBase
{
    private readonly DialogService dialogService = dialogService;

    [ObservableProperty]
    public partial AvaloniaList<EditorField> EditorFields { get; set; } = [];

    [ObservableProperty]
    public partial object? OwnerObject { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }
    /// <summary>
    ///     Gets or sets the field filter to use. If filter returns false, it will omit the field.
    /// </summary>
    public Func<PropertyInfo, bool> FieldAcceptFilter { get; set; } = _ => true;

    [ObservableProperty]
    public partial bool DisableButtons { get; set; }
    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task Save()
    {
        // All values are checked against the setting's own limits before any is applied: a value out of range would be
        // written to the file and the server would then refuse to start.
        List<(EditorField Field, object? Value)> converted = [];
        foreach (EditorField field in EditorFields)
        {
            if (field.Value is null && field.PropertyInfo.PropertyType.IsValueType)
            {
                continue;
            }

            object? value;
            try
            {
                value = Convert.ChangeType(field.Value, field.PropertyInfo.PropertyType);
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync(ex, description: field.ToString());
                return;
            }
            if (OwnerObject != null && GetValidationError(OwnerObject, field.PropertyInfo, value) is { } error)
            {
                await dialogService.ShowErrorAsync(new ValidationException(error), description: field.ToString());
                return;
            }
            converted.Add((field, value));
        }
        foreach ((EditorField field, object? value) in converted)
        {
            field.PropertyInfo.SetValue(OwnerObject, value);
        }
        Close(ButtonOptions.Ok);
    }

    /// <returns>Why <paramref name="value" /> breaks the limits declared on <paramref name="property" />, or null if it is valid.</returns>
    internal static string? GetValidationError(object owner, PropertyInfo property, object? value)
    {
        List<ValidationResult> results = [];
        ValidationContext context = new(owner) { MemberName = property.Name, DisplayName = property.Name };
        ValidationAttribute[] attributes = property.GetCustomAttributes<ValidationAttribute>(true).ToArray();
        if (Validator.TryValidateValue(value!, context, results, attributes))
        {
            return null;
        }
        return string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage));
    }

    public bool CanSave() => !HasErrors;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OwnerObject))
        {
            EditorFields.Clear();
            if (OwnerObject != null)
            {
                EditorFields.AddRange(OwnerObject
                                      .GetType()
                                      .GetProperties()
                                      .Where(FieldAcceptFilter)
                                      .Where(p => p.CanWrite)
                                      .Select(p => new EditorField(p, p.GetValue(OwnerObject), GetPossibleValues(p)))
                                      .Where(editorField => editorField.Value is string or bool or int or float || editorField.PossibleValues is [..]));
            }
        }
        base.OnPropertyChanged(e);
    }

    private static AvaloniaList<object> GetPossibleValues(PropertyInfo propertyInfo) =>
        propertyInfo.PropertyType.IsEnum ? new AvaloniaList<object>(propertyInfo.PropertyType.GetFields(BindingFlags.Static | BindingFlags.Public).Select(f => f.GetValue(propertyInfo.PropertyType))) : [];
}
