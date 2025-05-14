using System.Text;
using UnityEngine;

namespace YokiFrame
{
    public static class CodeGenHelper
    {
        public static GameObject GetBindBelongs2GameObject(AbstractBind bind)
        {
            var trans = bind.Transform;

            while (trans.parent != null)
            {
                if (trans.parent.GetComponent<IBindGroup>() != null)
                {
                    return trans.parent.gameObject;
                }

                trans = trans.parent;
            }

            return null;
        }

        public static string GetBindBelongs2(AbstractBind bind)
        {
            var trans = bind.Transform;

            while (trans.parent != null)
            {
                if (trans.parent.IsUIPanel())
                {
                    return "UIPanel" + "(" + trans.parent.name + ")";
                }


                trans = trans.parent;
            }

            return trans.name;
        }

        public static bool IsUIPanel(this Component component)
        {
            if (component.GetComponent("UIPanel"))
            {
                return true;
            }

            return false;
        }

        public static string PathToParent(Transform trans, string parentName)
        {
            string retValue = trans.name;

            while (trans.parent != null)
            {
                if (trans.parent.name.Equals(parentName))
                {
                    break;
                }

                retValue = $"{trans.parent.name}/{retValue}";

                trans = trans.parent;
            }

            return retValue;
        }
    }
}
