using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    public abstract class AknEditableListViewBase<TKey>
        : AknListViewBase<TKey>
        where TKey : notnull {

        [Parameter]
        public EventCallback<MyDataObj<TKey>> OnRowEditStarted { get; set; }

        [Parameter]
        public EventCallback<MyDataObj<TKey>> OnRowEditEnded { get; set; }

        [Parameter]
        public EventCallback<MyDataObj<TKey>> OnRowFocusLost { get; set; }

        protected MyDataObj<TKey>? FocusedItem;

        public virtual async Task BeginEdit(MyDataObj<TKey> item) {

            if (FocusedItem != null &&
                FocusedItem != item &&
                FocusedItem.IsEditing) {
                await EndEdit(FocusedItem);
            }

            foreach (var row in ListDataItems)
                row.IsEditing = false;

            item.IsEditing = true;

            FocusedItem = item;

            if (OnRowEditStarted.HasDelegate)
                await OnRowEditStarted.InvokeAsync(item);

            await InvokeAsync(StateHasChanged);

            await InvokeAsync(async () => {

                await Task.Delay(50);

                if (_listInputRef.Context != null)
                    await _listInputRef.FocusAsync();
            });
        }

        public virtual async Task EndEdit(
            MyDataObj<TKey> item,
            bool saveData = true) {
            if (!item.IsEditing)
                return;

            if (saveData &&
                OnRowFocusLost.HasDelegate) {
                await OnRowFocusLost.InvokeAsync(item);
            }

            item.IsEditing = false;

            if (FocusedItem == item)
                FocusedItem = null;

            if (OnRowEditEnded.HasDelegate)
                await OnRowEditEnded.InvokeAsync(item);

            await InvokeAsync(StateHasChanged);
        }

        public override async Task HandleListEditKey(
            KeyboardEventArgs e,
            MyDataObj<TKey> item) {
            if (e.Key == "Enter") {
                await EndEdit(item);
            } else if (e.Key == "Escape") {
                item.IsEditing = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        public virtual async Task HandleBlur(
            MyDataObj<TKey> item) {
            await EndEdit(item);
        }

        public override async Task HandleClick(
            MyDataObj<TKey> item,
            MouseEventArgs e) {
            if (FocusedItem != null &&
                FocusedItem != item &&
                FocusedItem.IsEditing) {
                await EndEdit(FocusedItem);
            }

            await base.HandleClick(item, e);
        }
    }
}