#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    public partial class YokiToolsWindow
    {
        #region 页面切换

        private void SelectPage(int index)
        {
            if (index < 0 || index >= mPageInfos.Count) return;

            var info = mPageInfos[index];

            if (mActivePage != default && mActivePageInfo.HasValue)
            {
                mActivePage.OnDeactivate();

                if (mSidebarItems.TryGetValue(mActivePageInfo.Value, out var oldItem))
                {
                    oldItem.RemoveFromClassList("selected");
                }
            }

            mSelectedPageIndex = index;
            var page = GetOrCreatePage(info);
            mActivePage = page;
            mActivePageInfo = info;

            if (mSidebarItems.TryGetValue(info, out var newItem))
            {
                newItem.AddToClassList("selected");

                var highlightColor = new Color(0.13f, 0.59f, 0.95f, 0.12f);
                mSidebarHighlight.style.backgroundColor = new StyleColor(highlightColor);

                newItem.schedule.Execute(() => MoveSidebarHighlight(newItem)).ExecuteLater(1);
            }

            ShowPageContent(info, page);
        }

        private void ShowPageContent(YokiPageInfo info, IYokiToolPage page)
        {
            mContentContainer.Clear();

            if (!mPageElements.TryGetValue(info, out var pageElement))
            {
                pageElement = page.CreateUI();
                mPageElements[info] = pageElement;
            }

            YokiStyleService.ApplyKitStyleToElement(pageElement, info.Kit);

            pageElement.AddToClassList("content-fade-in");
            pageElement.RemoveFromClassList("content-visible");

            mContentContainer.Add(pageElement);
            page.OnActivate();

            pageElement.schedule.Execute(() => pageElement.AddToClassList("content-visible"))
                .ExecuteLater(16);
        }

        private void PopoutPage(YokiPageInfo info)
        {
            var newPage = (IYokiToolPage)System.Activator.CreateInstance(info.PageType);
            YokiPagePopoutWindow.Open(newPage);
        }

        #endregion
    }
}
#endif