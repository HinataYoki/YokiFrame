using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public struct CustomUnRegister
    {
        private Action UnRegisterAction;
        public CustomUnRegister(Action UnRegister) => UnRegisterAction = UnRegister;

        public void UnRegister()
        {
            UnRegisterAction?.Invoke();
            UnRegisterAction = null;
        }
    }

    public abstract class UnRegisterTrigger : MonoBehaviour
    {
        private readonly HashSet<CustomUnRegister> mUnRegisters = new();

        public CustomUnRegister AddUnRegister(ref CustomUnRegister unRegister)
        {
            mUnRegisters.Add(unRegister);
            return unRegister;
        }

        public void RemoveUnRegister(CustomUnRegister unRegister) => mUnRegisters.Remove(unRegister);

        public void UnRegister()
        {
            foreach (var unRegister in mUnRegisters)
            {
                unRegister.UnRegister();
            }

            mUnRegisters.Clear();
        }
    }

    public class UnRegisterOnDestroyTrigger : UnRegisterTrigger
    {
        private void OnDestroy() => UnRegister();
    }

    public class UnRegisterOnDisableTrigger : UnRegisterTrigger
    {
        private void OnDisable() => UnRegister();
    }


    public static class UnRegisterExtension
    {
        static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var trigger = gameObject.GetComponent<T>();

            if (!trigger)
            {
                trigger = gameObject.AddComponent<T>();
            }

            return trigger;
        }


        public static CustomUnRegister UnRegisterWhenGameObjectDestroyed<T>(this CustomUnRegister self, T component) where T : Component =>
            self.UnRegisterWhenGameObjectDestroyed(component.gameObject);
        public static CustomUnRegister UnRegisterWhenGameObjectDestroyed(this CustomUnRegister unRegister, GameObject gameObject) =>
            GetOrAddComponent<UnRegisterOnDestroyTrigger>(gameObject).AddUnRegister(ref unRegister);

        public static CustomUnRegister UnRegisterWhenDisabled<T>(this CustomUnRegister self, T component) where T : Component =>
            self.UnRegisterWhenDisabled(component.gameObject);
        public static CustomUnRegister UnRegisterWhenDisabled(this CustomUnRegister unRegister, GameObject gameObject) =>
            GetOrAddComponent<UnRegisterOnDisableTrigger>(gameObject).AddUnRegister(ref unRegister);
    }
}