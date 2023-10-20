/*__ ____  _             
| __|_  / /_\  _ __ _ __ 
| _| / / / _ \| '_ \ '_ \
|___/___/_/ \_\ .__/ .__/
|  \/  |__ _| |_|__|_| _ 
| |\/| / _` | / / -_) '_|
|_|  |_\__,_|_\_\___|_|
 
(C)2022-2023 Derlidio Siqueira - Expoente Zero */

using EZAppMaker.Components;
using EZAppMaker.Defaults;

namespace EZForms
{
    public partial class EZSearch : EZContentView
    {
        private readonly EZCombo combo;
        private EZListEntry selected = null;
        private int awaiting = 0;
        private bool exit = false;

        public delegate void OnSelectHandler(EZListEntry entry);
        public event OnSelectHandler OnSelected;

        public EZSearch(List<EZListEntry> list)
        {
            InitializeComponent();

            combo = new EZCombo() { ItemsSource = list, Sorted = true, Key = "Key", Item = "Item" };
            combo.Placeholder = Default.Localization("ezsearch_placeholder");
            combo.OnItemSelected += Combo_OnItemSelected;
            combo.Unfocused += Combo_Unfocused;
            EZSearchStack.Children.Add(combo);
        }

        public override void ThemeChanged()
        {
            base.ThemeChanged();

            SearchFrame.Background = Default.Brush("ezsearch_background");
            SearchFrame.BorderColor = Default.Color("ezsearch_border");
        }

        public new void Focus()
        {
            combo.Focus();
        }

        private void Combo_OnItemSelected(EZCombo combo, object selected)
        {
            IsVisible = false;
            this.selected = (EZListEntry)selected;
            exit = true;

            _ = Return();
        }

        private void Combo_Unfocused(EZCombo combo)
        {
            // ------------------------------------------------------
            //                       IMPORTANT
            // ------------------------------------------------------
            // We want the search window to close on two scenarios:
            //
            // * when the user selects something from the list
            // * when the filtered list component (combo) loses focus
            //
            // The problem: both scenarios must call the same delegate
            // on the caller form. If both events occur, we would call
            // the delegate twice (wich would cause a crash). So, lets
            // tunel both events to the same exit point: the Return()
            // Task, which will handle multiple entrances and treat
            // them accordingly.
            // ------------------------------------------------------

            IsVisible = false;

            if (!exit)
            {
                _ = Return();
            }
        }

        private async Task Return()
        {
            if (0 == Interlocked.Exchange(ref awaiting, 1))
            {
                await Task.Delay(250); // Time to process Unfocus

                OnSelected?.Invoke(selected);
            }
        }
    }
}