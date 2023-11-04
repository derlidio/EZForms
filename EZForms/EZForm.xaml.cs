/*__ ____  _             
| __|_  / /_\  _ __ _ __ 
| _| / / / _ \| '_ \ '_ \
|___/___/_/ \_\ .__/ .__/
|  \/  |__ _| |_|__|_| _ 
| |\/| / _` | / / -_) '_|
|_|  |_\__,_|_\_\___|_|
 
(C)2022 Derlidio Siqueira - Expoente Zero */

// There is some refactoring to be done on the Build and Bind
// private methods. They are repeating too much code. I'm not
// happy with that :(

using System.Reflection;

using EZAppMaker.Attributes;
using EZAppMaker.Components;
using EZAppMaker.Converters;
using EZAppMaker.Defaults;
using EZAppMaker.Interfaces;
using EZAppMaker.Support;

namespace EZForms
{
    public partial class EZForm : EZContentView
    {
        private readonly Dictionary<string, IEZComponent> contexts = new Dictionary<string, IEZComponent>();
        private readonly Dictionary<string, object> parameters;

        private readonly string database;
        private readonly string form;

        private bool suspend_checks;
        private bool new_record;
        private bool modified;

        private int floaters = 0;

        private EZDataset dataset;
        private EZSearch search;

        public EZForm(string id, string database, string form, Dictionary<string, object> parameters = null)
        {
            BindingContext = this;

            ItemId = id;
            
            this.database = database;
            this.form = form;
            this.parameters = parameters;

            InitializeComponent();

            EZFormGrid.Children.Remove(NavigationBar);
            EZFormGrid.Children.Remove(ButtonBar);

            NavSlider.ValueChanged += Slider_ValueChanged;
            NavSlider.DragCompleted += Slider_DragCompleted;

            SliderTrackMin.SizeChanged += Track_SizeChanged;

            Build();
        }

        private void Track_SizeChanged(object sender, EventArgs e)
        {
            SliderTrackMax.WidthRequest = SliderTrackMin.Width * NavSlider.Value + 1; // + 1 -> iOS WORKAROUND
            SliderPointer.TranslationX = (PointerGrid.Width - SliderPointer.Width) * NavSlider.Value;
        }

        //   _  _          _           _   _          
        //  | \| |__ ___ _(_)__ _ __ _| |_(_)___ _ _  
        //  | .` / _` \ V / / _` / _` |  _| / _ \ ' \ 
        //  |_|\_\__,_|\_/|_\__, \__,_|\__|_\___/_||_|
        //                  |___/

        [NavigationEventHandler]
        public override void OnAppearing()
        {
            base.OnAppearing();

            AddFloaters();
            SetButtons();
        }

        [NavigationEventHandler]
        public override void OnRaised()
        {
            base.OnRaised();

            AddFloaters();
        }

        [NavigationEventHandler]
        public override bool OnBeforeHiding()
        {
            bool ok = base.OnBeforeHiding();
            RemoveFloaters();
            return ok;
        }

        [NavigationEventHandler]
        public override void OnHidden()
        {
            base.OnHidden();

            RemoveFloaters();
        }

        [NavigationEventHandler]
        public override void OnLeaving()
        {
            base.OnLeaving();

            dataset?.Dispose();
            dataset = null;
        }

        [NavigationEventHandler]
        public override bool OnBeforeLeaving()
        {
            if (modified)
            {
                EZApp.Alert(Default.Localization("ezforms_record_changed"));
                return false;
            }

            bool ok = base.OnBeforeLeaving();
            RemoveFloaters();
            return ok;
        }

        public override void ThemeChanged()
        {
            base.ThemeChanged();
            
            ThemeFloater(NavigationBar, "ezforms_navigationbar");
            ThemeFloater(ButtonBar, "ezforms_buttonbar");

            NavBarSeparator.Color = Default.Color("ezforms_navigationbarseparator");
            ButtonBarSeparator.Color = Default.Color("ezforms_buttonbarseparator");

            search?.ThemeChanged();
        }

        //  ___     _          _         __  __           _                
        // | _ \_ _(_)_ ____ _| |_ ___  |  \/  |___ _ __ | |__  ___ _ _ ___
        // |  _/ '_| \ V / _` |  _/ -_) | |\/| / -_) '  \| '_ \/ -_) '_(_-<
        // |_| |_| |_|\_/\__,_|\__\___| |_|  |_\___|_|_|_|_.__/\___|_| /__/

        private void AddFloaters()
        {
            if (0 == Interlocked.Exchange(ref floaters, 1))
            {
                EZApp.Container.AddFloater(NavigationBar);
                EZApp.Container.AddFloater(ButtonBar);

                EZApp.Container.TopOffset += NavigationBar.HeightRequest;
            }
        }

        private void ThemeFloater(Grid floater, string background)
        {
            floater.BackgroundColor = Default.Color(background);

            foreach (VisualElement v in EZXamarin.GetChildren<VisualElement>(floater))
            {
                MethodInfo method = v.GetType().GetMethod("ThemeChanged");

                method?.Invoke(v, new object[] { });
            }
        }

        private void RemoveFloaters()
        {
            if (1 == Interlocked.Exchange(ref floaters, 0))
            {
                EZApp.Container.RemoveFloater(NavigationBar);
                EZApp.Container.RemoveFloater(ButtonBar);

                EZApp.Container.TopOffset -= NavigationBar.Height;
            }
        }

        private void Build()
        {
            EZFormTag frm = EZFormsBuilder.GetForm(form);

            if (frm == null) return;

            Title = frm.Label;

            BuildDataSet(frm);

            if (dataset == null) return;

            if (!string.IsNullOrWhiteSpace(frm.Class))
            {
                suspend_checks = true;
                BuildClass(frm);
                suspend_checks = false;

                return;
            }

            suspend_checks = true;
            BuildForm(frm);
            suspend_checks = false;
        }

        private void BuildDataSet(EZFormTag frm)
        {
            if (!string.IsNullOrWhiteSpace(frm?.Source))
            {
                dataset = new EZDataset(database, frm.Source, parameters, frm.Order, 1);
                
                ShowPageNumber();
            }
        }

        private ColumnDefinition DefineGridColumn(EZFieldTag field)
        {
            ColumnDefinition col = new ColumnDefinition();

            _ = double.TryParse(field?.Width.ToString(), out double width);

            if (width >= 0)
            {
                if (width <= 1) // Percent: 0 to 1
                {
                    col.Width = new GridLength(width, GridUnitType.Star);
                }
                else
                {
                    col.Width = new GridLength(width, GridUnitType.Absolute);
                }
            }

            return col;
        }

        private string DefineLabel(EZFieldTag field)
        {
            string label = null;

            if (field?.Label != null)
            {
                string lbl = field.Label.Trim();

                if ((lbl != "") && (lbl.Substring(0, 1) == "{") && (lbl.Substring(lbl.Length - 1, 1) == "}"))
                {
                    lbl = lbl.Replace("{", "").Replace("}", "");

                    if (lbl != "")
                    {
                        label = dataset.Value(lbl)?.ToString(); // Use another column value as label
                    }
                }
                else
                {
                    label = field.Label;
                }
            }

            return label;
        }

        private double DefineWidthRequest(EZFieldTag field)
        {
            _ = double.TryParse(field?.Width.ToString(), out double width);

            return width;
        }

        private double DefineHeightRequest(EZFieldTag field)
        {
            _ = double.TryParse(field?.Height.ToString(), out double height);

            return height;
        }

        private Keyboard DefineKeyboard(string column)
        {
            Type type = dataset.DefaultType(column);

            if ((type == typeof(int)) || (type == typeof(long)) || (type == typeof(double)))
            {
                return Keyboard.Numeric;
            }

            return Keyboard.Plain;
        }

        private Binding CreateBinding(IValueConverter converter = null, object parameter = null)
        {
            Binding binding = new Binding()
            {
                Source = null,
                Path = ".",
                Converter = converter,
                ConverterParameter = parameter,
                Mode = BindingMode.OneTime
            };
    
            return binding;
        }

        private List<EZListEntry> BuildComboItemsSource(EZCombo combo, string list)
        {
            List<EZListEntry> items = null;

            if (EZFormsBuilder.ListExists(list))
            {
                EZListTag lst = EZFormsBuilder.GetList(list);

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                foreach (EZFilterTag filter in lst.Filters)
                {
                    if (contexts.ContainsKey(filter.Source))
                    {
                        parameters.Add(lst.Source, contexts[lst.Source].ToDatabaseValue(dataset.DefaultValue(lst.Source)));
                    }
                }

                using (EZDataset ds = new EZDataset(database, lst.Source, parameters, null, 0))
                {
                    if (ds != null)
                    {
                        items = ds.ToListItemsSource(lst.Item, lst.Key, lst.Detail, lst.Group);

                        if (ds.ColumnExists(lst.Key)) combo.Key = "Key";
                        if (ds.ColumnExists(lst.Item)) combo.Item = "Item";
                        if (ds.ColumnExists(lst.Detail)) combo.Detail = "Detail";
                        if (ds.ColumnExists(lst.Group)) combo.Group = "Group";
                    }
                }
            }

            return items;
        }

        private List<EZListEntry> BuildSearchableItemsSource(EZCombo combo, string column)
        {
            List<EZListEntry> items = dataset.ToSearchItemsSource(column);

            combo.Key = "Key";
            combo.Item = "Item";

            return items;
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            int value = ((int)((Slider)sender).Value);

            value = (dataset.Pages - 1) * value;

            SliderTrackMax.WidthRequest = SliderTrackMin.Width * NavSlider.Value + 1; // + 1 -> iOS WORKAROUND
            SliderPointer.TranslationX = (PointerGrid.Width - SliderPointer.Width) * NavSlider.Value;

            CurrentPage.Text = $"{value + 1}/{dataset.Pages}";
        }

        private void Slider_DragCompleted(object sender, EventArgs e)
        {
            int value = (int)((dataset.Pages - 1) * NavSlider.Value);

            SliderTrackMax.WidthRequest = SliderTrackMin.Width * NavSlider.Value + 1; // + 1 -> iOS WORKAROUND
            SliderPointer.TranslationX = (PointerGrid.Width - SliderPointer.Width) * NavSlider.Value;

            if (dataset.FetchPage(value))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            ShowPageNumber();

            suspend_checks = true;

            foreach (KeyValuePair<string, IEZComponent> pair in contexts)
            {
                ((View)(pair.Value)).BindingContext = dataset.Value(pair.Key);
            }

            suspend_checks = false;
        }

        private void ShowPageNumber()
        {
            int offset = (dataset.Pages > 0) ? 1 : 0;

            CurrentPage.Text = $"{dataset.Page + offset}/{dataset.Pages}";
        }

        private void UpdateSlider()
        {
            NavSlider.Value = (double)dataset.Page / (dataset.Pages - 1);
        }

        private void ClearFields()
        {
            suspend_checks = true;

            foreach (KeyValuePair<string, IEZComponent> pair in contexts)
            {
                ((View)(pair.Value)).BindingContext = dataset.DefaultValue(pair.Key);
                pair.Value.Clear();
            }

            suspend_checks = false;
        }

        private void DisableNavigation()
        {
            NavigationBar.IsEnabled = false; // <-- This should disable everything inside, but...

            NavFirst.Opacity = 0.5D;
            NavPrevious.Opacity = 0.5D;
            NavNext.Opacity = 0.5D;
            NavLast.Opacity = 0.5D;

            // .NET 8 IsEnabled property is broken. It does not propagate
            // the status to layout children. So... we have to workaround.

            NavFirst.IsEnabled = false;
            NavPrevious.IsEnabled = false;
            NavNext.IsEnabled = false;
            NavLast.IsEnabled = false;

            NavSlider.IsEnabled = false;
        }

        private void EnableNavigation()
        {
            NavigationBar.IsEnabled = true;

            NavFirst.Opacity = 1.0D; 
            NavPrevious.Opacity = 1.0D;
            NavNext.Opacity = 1.0D;
            NavLast.Opacity = 1.0D;

            // .NET 8 IsEnabled property is broken. It does not propagate
            // the status to layout children. So... we have to workaround.

            NavFirst.IsEnabled = true;
            NavPrevious.IsEnabled = true;
            NavNext.IsEnabled = true;
            NavLast.IsEnabled = true;

            NavSlider.IsEnabled = true;
        }

        private void SetButtons()
        {
            if (new_record || modified)
            {
                DisableNavigation();
            }
            else
            {
                EnableNavigation();
            }

            ButtonAdd.IsVisible = !new_record;
            ButtonCancel.IsVisible = new_record;

            ButtonAdd.IsEnabled = !modified;
            ButtonDelete.IsEnabled = !new_record && !modified;
            ButtonRestore.IsEnabled = modified;
            ButtonCancel.IsEnabled = new_record;
            ButtonSave.IsEnabled = modified;

            ButtonAdd.Opacity = ButtonAdd.IsEnabled ? 1.0D : 0.5D;
            ButtonDelete.Opacity = ButtonDelete.IsEnabled ? 1.0D : 0.5D;
            ButtonRestore.Opacity = ButtonRestore.IsEnabled ? 1.0D : 0.5D;
            ButtonSave.Opacity = ButtonSave.IsEnabled ? 1.0D : 0.5D;
            ButtonJump.Opacity = ButtonJump.IsEnabled ? 1.0D : 0.5D;
        }

        private void CheckChanges(IEZComponent component)
        {
            if (suspend_checks) return;

            CheckFilters(component);

            modified = false;

            foreach(KeyValuePair<string, IEZComponent> pair in contexts)
            {
                if (pair.Value.Modified())
                {
                    modified = true;
                    break;
                }
            }

            SetButtons();
        }

        private void CheckFilters(IEZComponent component)
        {
            if (component == null) return;

            foreach (KeyValuePair<string, IEZComponent> pair in contexts)
            {
                EZFieldTag fld = EZFormsBuilder.GetField(form, pair.Key);

                if (fld.Filters.Count == 0) continue;

                EZFilterTag filter = fld.Filters.Find((x) => x.Source == component.ItemId);

                if (filter == null) continue;

                pair.Value.Clear();

                if (pair.Value.GetType() == typeof(EZCombo))
                {
                    EZCombo combo = (EZCombo)pair.Value;
                    combo.ItemsSource = BuildComboItemsSource(combo, fld.List);
                }
            }
        }

        //  ___ _______                    ___                            ___             _      
        // | __|_  / __|__ _ _ _ __  ___  / __|___ _ __  _ __  ___ _ _   | __|_ _____ _ _| |_ ___
        // | _| / /| _/ _ \ '_| '  \(_-< | (__/ _ \ '  \| '  \/ _ \ ' \  | _|\ V / -_) ' \  _(_-<
        // |___/___|_|\___/_| |_|_|_/__/  \___\___/_|_|_|_|_|_\___/_||_| |___|\_/\___|_||_\__/__/

        private void OnEZEntryChanged(EZEntry entry, TextChangedEventArgs args) { CheckChanges(entry); }
        private void OnEZComboChanged(EZCombo combo, object selected) { CheckChanges(combo); }
        private void OnEZCheckBoxChanged(EZCheckBox checkbox) { CheckChanges(checkbox); }
        private void OnEZSignatureChanged(EZSignature signature) { CheckChanges(signature); }
        private void OnEZPhotoChanged(EZPhoto photo) { CheckChanges(photo); }
        private void OnEZColorPickerChanged(EZColorPicker picker) { CheckChanges(picker); }
        private void OnEZSliderChanged(EZSlider slider) { CheckChanges(slider); }
        private void OnEZRatingChanged(EZRating rating) { CheckChanges(rating); }
        private void OnEZRadioButtonChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender == null) return;
            EZRadioButton radio = (EZRadioButton)sender;
            CheckChanges(radio.Group);
        }

        [XamlEventHandler]
        private void Handle_Navigation_Tap(object sender, EventArgs e)
        {
            if (dataset == null) return;

            Grid button = (Grid)sender;

            // .NET 8 IsEnabled property is broken. It does not propagate
            // the status to layout children. So... we have to workaround.

            if (!button.IsEnabled) return; /* WORKAROUND */

            bool fetch = false;

            switch (button.ClassId)
            {
                case "F": fetch = dataset.FetchFirstPage(); break;
                case "P": fetch = dataset.FetchPrevPage(); break;
                case "N": fetch = dataset.FetchNextPage(); break;
                case "L": fetch = dataset.FetchLastPage(); break;
            }

            UpdateSlider();

            if (fetch)
            {
                Refresh();
            }
        }

        [ComponentEventHandler]
        private void Handle_CRUD_Tap(object sender, EventArgs e)
        {
            Grid button = (Grid)sender;

            if (!button.IsEnabled) return; /* WORKAROUND */

            EZApp.Container.HideKeyboard();

            switch(button.ClassId)
            {
                case "C": OnAddTap(); break;
                case "R": OnRestoreTap(); break;
                case "U": OnSaveTap(); break;
                case "D": OnDeleteTap(); break;
                case "X": OnCancelTap(); break;
                case "J": OnRestoreTap(); break;
            }
        }

        private async void OnDeleteTap()
        {
            bool yes = await EZApp.Question(Default.Localization("ezforms_delete"));

            if (!yes) return;
            
            if (dataset.Delete(contexts) > 0)
            {
                Refresh();
                UpdateSlider();
            }
        }

        private void OnRestoreTap()
        {
            modified = false;
            ClearFields();
            if (!new_record) Refresh();
            SetButtons();
        }

        private void OnAddTap()
        {
            new_record = true;
            ClearFields();
            SetButtons();

            EZApp.Container.Scroll(0);
        }

        private void OnCancelTap()
        {
            new_record = false;
            modified = false;
            ClearFields();
            Refresh();
            SetButtons();
        }

        private void OnJumpTap()
        {
            /* TBD */
        }

        private void OnSaveTap()
        {
            bool ok = EZApp.ValidateRequired(this, out VisualElement failed);

            if (!ok) return;

            int result = new_record || (dataset.Count == 0) ? dataset.Insert(contexts) : dataset.Update(contexts);

            if (result == 1)
            {
                Refresh();
                UpdateSlider();
                new_record = false;
                modified = false;

                SetButtons();
            }
        }

        private void Handle_SearchTap(EZEntry entry)
        {
            NavigationBar.IsEnabled = false;
            ButtonBar.IsEnabled = false;

            string column = entry.ItemId;
            List<EZListEntry> list = dataset.ToSearchItemsSource(column);

            EZFormStack.IsVisible = false;

            search = new EZSearch(list);
            search.OnSelected += OnEZSearchableSelected;

            EZSearchGrid.Add(search, 0, 0);
            EZSearchGrid.IsVisible = true;

            search.Focus();
        }

        [ComponentEventHandler]
        private void OnEZSearchableSelected(EZListEntry entry)
        {
            EZSearchGrid.IsVisible = false;
            EZSearchGrid.Children.Clear();

            if ((entry != null) && (entry.Key != null) && dataset.FetchRowId((long)entry.Key))
            {
                UpdateSlider();
                Refresh();
            }

            EZFormStack.IsVisible = true;
            NavigationBar.IsEnabled = true;
            ButtonBar.IsEnabled = true;

            search = null;

            GC.Collect(); // Try to keep memory comsumption low by collecting the search list.
        }

        //  ___                 __                 ___ ______  __          _              
        // | __|__ _ _ _ __    / _|_ _ ___ _ __   | __|_  /  \/  |__ _ _ _| |___  _ _ __
        // | _/ _ \ '_| '  \  |  _| '_/ _ \ '  \  | _| / /| |\/| / _` | '_| / / || | '_ \
        // |_|\___/_| |_|_|_| |_| |_| \___/_|_|_| |___/___|_|  |_\__,_|_| |_\_\\_,_| .__/
        //                                                                         |_|

        private void BuildForm(EZFormTag frm)
        {
            EZFieldTag fld;
            Grid grd = null;
            int col = 0;

            foreach (string column in dataset.Columns)
            {
                fld = EZFormsBuilder.GetField(form, column);

                IEZComponent element = BuildElement(fld);

                if (element == null) continue;

                contexts.Add(column, element);

                if (!fld.LineBreak)
                {
                    if (grd == null)
                    {
                        grd = new Grid() { ColumnSpacing = EZSettings.FormsColumnSpacing};
                    }

                    grd.ColumnDefinitions.Add(DefineGridColumn(fld));
                    grd.Add((View)element, col, 0);
                    col++;

                    continue;
                }

                if (grd != null)
                {
                    grd.ColumnDefinitions.Add(DefineGridColumn(fld));
                    grd.Add((View)element, col, 0);
                    EZFormStack.Add(grd);

                    grd = null;
                    col = 0;

                    continue;
                }

                EZFormStack.Add((View)element);
            }

            EZFormStack.Add(new EZExpander());
        }

        private IEZComponent BuildElement(EZFieldTag field)
        {
            IEZComponent element = null;

            if (field != null)
            {
                switch (EZFormsBuilder.GetInputType(field))
                {
                    case "entry": element = BuildEntry(field); break;
                    case "combo": element = BuildCombo(field); break;
                    case "check": element = BuildCheckBox(field); break;
                    case "radio": element = BuildRadioGroup(field); break;
                    case "photo": element = BuildPhoto(field); break;
                    case "color": element = BuildColorPicker(field); break;
                    case "slider": element = BuildSlider(field); break;
                    case "rating": element = BuildRating(field); break;
                    case "signature": element = BuildSignature(field); break;
                }
            }

            return element;
        }

        private IEZComponent BuildEntry(EZFieldTag field)
        {
            if (field == null) return null;

            if (!string.IsNullOrWhiteSpace(field.List))
            {
                return BuildCombo(field);
            }

            EZEntry entry = new EZEntry();

            entry.Detached = field.IsDetached;

            entry.ItemId = field.Source;
            entry.Label = DefineLabel(field);
            entry.Placeholder = field.Placeholder;
            entry.Mask = field.Mask;
            entry.IsReadOnly = field.IsReadOnly;
            entry.IsRequired = field.IsRequired;

            if (!string.IsNullOrWhiteSpace(field.Keyboard))
            {
                entry.Keyboard = EZFormsBuilder.KeyboardType(field.Keyboard);
            }
            else
            {
                entry.Keyboard = DefineKeyboard(field.Source);
            }

            if ((field.Length > 0) && string.IsNullOrWhiteSpace(field.Mask))
            {
                entry.MaxLength = field.Length;
            }

            if (field.IsSearchable)
            {
                entry.IsSearchable = true;
                entry.OnSearchTapped += Handle_SearchTap;
            }

            entry.OnChanged += OnEZEntryChanged;

            entry.SetBinding(EZEntry.TextProperty, ".");
            entry.BindingContext = dataset.Value(field.Source);

            return entry;
        }

        private IEZComponent BuildCombo(EZFieldTag field)
        {
            EZCombo combo = null;

            if (field == null) return combo;

            combo = new EZCombo();

            combo.Detached = field.IsDetached;

            combo.ItemId = field.Source;
            combo.Label = field.Label;
            combo.Placeholder = field.Placeholder;
            combo.IsReadOnly = field.IsReadOnly;
            combo.IsRequired = field.IsRequired;
            combo.Mask = field.Mask;

            if (!string.IsNullOrWhiteSpace(field.Keyboard))
            {
                combo.Keyboard = EZFormsBuilder.KeyboardType(field.Keyboard);
            }
            else
            {
                combo.Keyboard = Keyboard.Plain;
            }

            if (field.Length > 0)
            {
                combo.MaxLength = field.Length;
            }

            combo.ItemsSource = BuildComboItemsSource(combo, field.List);
            combo.OnItemSelected += OnEZComboChanged;

            combo.SetBinding(EZCombo.RawValueProperty, ".");
            combo.BindingContext = dataset.Value(field.Source);

            return combo;
        }

        private IEZComponent BuildCheckBox(EZFieldTag field)
        {
            EZCheckBox check = null;

            if (field == null) return check;

            check = new EZCheckBox();

            check.Detached = field.IsDetached;

            check.ItemId = field.Source;
            check.Label = DefineLabel(field);

            check.OnChange += OnEZCheckBoxChanged;

            check.SetBinding(EZCheckBox.IsCheckedProperty, ".", BindingMode.TwoWay, new EZLongToBoolConverter());
            check.BindingContext = dataset.Value(field.Source);

            return check;
        }

        private IEZComponent BuildRadioGroup(EZFieldTag field, EZRadioGroup group = null)
        {
            EZListTag lst = EZFormsBuilder.GetList(field.List);

            if (lst == null) return group;

            using (EZDataset ds = new EZDataset(database, lst.Source, null, null, 0))
            {
                if (ds != null)
                {
                    List<EZListEntry> items = ds.ToListItemsSource(lst.Item, lst.Key, lst.Detail, lst.Group);

                    if (items != null)
                    {
                        if (group == null)
                        {
                            group = new EZRadioGroup() { ItemId = field.Source, Detached = field.IsDetached, Spacing = 5 };
                        }

                        foreach (EZListEntry entry in items)
                        {
                            EZRadioButton radio = new EZRadioButton();

                            radio.Group = group;
                            radio.Label = entry.Item.ToString();
                            radio.Value = entry.Key;
                            radio.HorizontalOptions = LayoutOptions.Start;

                            radio.CheckedChanged += OnEZRadioButtonChanged;

                            var binding = CreateBinding
                            (
                                new EZIsEqualConverter(),
                                entry.Key
                            );

                            binding.Mode = BindingMode.TwoWay;
                            
                            radio.SetBinding(RadioButton.IsCheckedProperty, binding);

                            group.Add(radio);
                        }

                        group.BindingContext = dataset.Value(field.Source);
                    }
                }
            }

            return group;
        }

        private IEZComponent BuildPhoto(EZFieldTag field)
        {
            EZPhoto photo = null;

            if (field == null) return photo;

            photo = new EZPhoto();

            photo.Detached = field.IsDetached;

            photo.ItemId = field.Source;
            photo.Label = DefineLabel(field);
            photo.HeightRequest = DefineHeightRequest(field);

            photo.OnChanged += OnEZPhotoChanged;

            photo.SetBinding(EZPhoto.PhotoFileProperty, ".");
            photo.BindingContext = dataset.Value(field.Source);

            return photo;
        }

        private IEZComponent BuildColorPicker(EZFieldTag field)
        {
            EZColorPicker picker = null;

            if (field == null) return picker;

            picker = new EZColorPicker();

            picker.Detached = field.IsDetached;

            picker.ItemId = field.Source;
            picker.Label = DefineLabel(field);

            picker.OnChanged += OnEZColorPickerChanged;

            picker.SetBinding(EZColorPicker.ColorValueProperty, ".");
            picker.BindingContext = dataset.Value(field.Source);

            return picker;
        }

        private IEZComponent BuildSlider(EZFieldTag field)
        {
            EZSlider slider = null;

            if (field == null) return slider;
            
            slider = new EZSlider();

            slider.Detached = field.IsDetached;

            slider.ItemId = field.Source;
            slider.Label = DefineLabel(field);
            slider.Decimals = field.Decimals;
            slider.Min = field.Min;
            slider.Max = field.Max;

            slider.OnDragCompleted += OnEZSliderChanged;

            slider.SetBinding(EZSlider.ValueProperty, ".");
            slider.BindingContext = dataset.Value(field.Source);

            return slider;
        }

        private IEZComponent BuildRating(EZFieldTag field)
        {
            EZRating rating = null;

            if (field == null) return rating;
            
            rating = new EZRating();

            rating.Detached = field.IsDetached;

            rating.ItemId = field.Source;
            rating.Label = DefineLabel(field);

            rating.OnChanged += OnEZRatingChanged;

            rating.SetBinding(EZRating.RatingProperty, ".");
            rating.BindingContext = dataset.Value(field.Source);

            return rating;
        }

        private IEZComponent BuildSignature(EZFieldTag field)
        {
            EZSignature signature = null;

            if (field == null) return signature;
            
            signature = new EZSignature();

            signature.Detached = field.IsDetached;

            signature.ItemId = field.Source;
            signature.Label = DefineLabel(field);
            signature.HeightRequest = DefineHeightRequest(field);

            if (field.IsRequired) signature.IsRequired = field.IsRequired;

            signature.OnChanged += OnEZSignatureChanged;

            signature.SetBinding(EZSignature.DataProperty, ".");
            signature.BindingContext = dataset.Value(field.Source);

            return signature;
        }

        //  ___                 __                  ___ _            
        // | __|__ _ _ _ __    / _|_ _ ___ _ __    / __| |__ _ ______
        // | _/ _ \ '_| '  \  |  _| '_/ _ \ '  \  | (__| / _` (_-<_-<
        // |_|\___/_| |_|_|_| |_| |_| \___/_|_|_|  \___|_\__,_/__/__/

        private void BuildClass(EZFormTag frm)
        {
            EZContentView view = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic)
                {
                    try
                    {
                        foreach (Type type in assembly.GetExportedTypes())
                        {
                            if (type.Name == frm.Class)
                            {
                                view = (EZContentView)Activator.CreateInstance(type);
                                break;
                            }
                        }
                    }
                    catch { /* Dismiss */ }
                }
            }

            if ((view == null) || (view.Content == null)) return;

            foreach(Element element in EZXamarin.GetChildren<Element>(view))
            {
                Type type = element.GetType();

                System.Diagnostics.Debug.WriteLine(type.ToString());

                if (type == typeof(EZEntry))
                {
                    BindEntry((EZEntry)element);
                    continue;
                }

                if (type == typeof(EZCombo))
                {
                    BindCombo((EZCombo)element);
                    continue;
                }

                if (type == typeof(EZCheckBox))
                {
                    BindCheckBox((EZCheckBox)element);
                    continue;
                }

                if (type == typeof(EZRadioGroup))
                {
                    BindRadioGroup((EZRadioGroup)element);
                    continue;
                }

                if (type == typeof(EZSignature))
                {
                    BindSignature((EZSignature)element);
                    continue;
                }

                if (type == typeof(EZPhoto))
                {
                    BindPhoto((EZPhoto)element);
                    continue;
                }

                if (type == typeof(EZSlider))
                {
                    BindSlider((EZSlider)element);
                    continue;
                }

                if (type == typeof(EZRating))
                {
                    BindRating((EZRating)element);
                    continue;
                }

                if (type == typeof(EZColorPicker))
                {
                    BindColorPicker((EZColorPicker)element);
                    continue;
                }
            }

            EZFormStack.Add(view);
        }

        private void BindEntry(EZEntry entry)
        {
            string column = entry.ItemId;

            if (dataset.ColumnExists(column))
            {
                EZFieldTag field = EZFormsBuilder.GetField(form, column);

                if (field != null)
                {
                    entry.Detached = field.IsDetached;

                    if (entry.Label == null) entry.Label = DefineLabel(field);
                    if (entry.Placeholder == null) entry.Placeholder = field.Placeholder;
                    if (entry.Mask == null) entry.Mask = field.Mask;

                    if (field.IsRequired) entry.IsRequired = field.IsRequired;
                    if (field.Length != 0) entry.MaxLength = field.Length;
                    if (field.Keyboard != "") entry.Keyboard = EZFormsBuilder.KeyboardType(field.Keyboard);

                    entry.OnChanged += OnEZEntryChanged;

                    entry.SetBinding(EZEntry.TextProperty, ".");
                    entry.BindingContext = dataset.Value(column);

                    contexts.Add(column, entry);
                }
            }
        }

        private void BindCombo(EZCombo combo)
        {
            string column = combo.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            combo.Detached = field.IsDetached;

            if (combo.Label == null) combo.Label = DefineLabel(field);
            if (combo.Placeholder == null) combo.Placeholder = field.Placeholder;
            if (combo.Mask == null) combo.Mask = field.Mask;

            if (field.IsRequired) combo.IsRequired = field.IsRequired;
            if (field.Length != 0) combo.MaxLength = field.Length;
            if (field.Keyboard != "") combo.Keyboard = EZFormsBuilder.KeyboardType(field.Keyboard);

            combo.ItemsSource = BuildComboItemsSource(combo, field.List);

            combo.OnItemSelected += OnEZComboChanged;

            combo.SetBinding(EZCombo.RawValueProperty, ".");
            combo.BindingContext = dataset.Value(column);

            contexts.Add(column, combo);
        }

        private void BindCheckBox(EZCheckBox check)
        {
            string column = check.ItemId;

            if (!dataset.ColumnExists(column)) return;

            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            check.Detached = field.IsDetached;

            if (string.IsNullOrWhiteSpace(check.Label)) check.Label = DefineLabel(field);

            check.OnChange += OnEZCheckBoxChanged;

            check.SetBinding(EZCheckBox.IsCheckedProperty, ".", BindingMode.TwoWay, new EZLongToBoolConverter());
            check.BindingContext = dataset.Value(column);

            contexts.Add(column, check);
        }

        private void BindRadioGroup(EZRadioGroup group)
        {
            string column = group.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            group.Detached = field.IsDetached;

            _ = BuildRadioGroup(field, group);

            contexts.Add(column, group);
        }

        private void BindSignature(EZSignature signature)
        {
            string column = signature.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            signature.Detached = field.IsDetached;

            if (signature.HeightRequest == -1) signature.HeightRequest = DefineHeightRequest(field);
            if (signature.Label == "") signature.Label = DefineLabel(field);

            signature.OnChanged += OnEZSignatureChanged;

            signature.SetBinding(EZSignature.DataProperty, ".");
            signature.BindingContext = dataset.Value(column);

            contexts.Add(column, signature);
        }

        private void BindPhoto(EZPhoto photo)
        {
            string column = photo.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            photo.Detached = field.IsDetached;

            if (photo.HeightRequest == -1) photo.HeightRequest = DefineHeightRequest(field);
            if (photo.Label == "") photo.Label = DefineLabel(field);

            photo.OnChanged += OnEZPhotoChanged;

            photo.SetBinding(EZPhoto.PhotoFileProperty, ".");
            photo.BindingContext = dataset.Value(column);

            contexts.Add(column, photo);
        }

        private void BindSlider(EZSlider slider)
        {
            string column = slider.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            slider.Detached = field.IsDetached;

            if (slider.Label == "") slider.Label = DefineLabel(field);

            if (slider.Min == 0) slider.Min = field.Min;
            if (slider.Max == 0) slider.Max = field.Max;

            slider.OnDragCompleted += OnEZSliderChanged;

            slider.SetBinding(EZSlider.ValueProperty, ".");
            slider.BindingContext = dataset.Value(column);

            contexts.Add(column, slider);
        }

        private void BindRating(EZRating rating)
        {
            string column = rating.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            rating.Detached = field.IsDetached;

            if (rating.Label == "") rating.Label = DefineLabel(field);

            rating.OnChanged += OnEZRatingChanged;

            rating.SetBinding(EZRating.RatingProperty, ".");
            rating.BindingContext = dataset.Value(column);

            contexts.Add(column, rating);
        }

        private void BindColorPicker(EZColorPicker picker)
        {
            string column = picker.ItemId;

            if (!dataset.ColumnExists(column)) return;
            
            EZFieldTag field = EZFormsBuilder.GetField(form, column);

            if (field == null) return;
            
            picker.Detached = field.IsDetached;

            if (picker.Label == "") picker.Label = DefineLabel(field);
                    
            picker.OnChanged += OnEZColorPickerChanged;

            picker.SetBinding(EZColorPicker.ColorValueProperty, ".");
            picker.BindingContext = dataset.Value(column);

            contexts.Add(column, picker);
        }
    }
}