using System.Diagnostics;

namespace GameLogic
{
    /// <summary>
    /// 全局对象必须继承于此。
    /// </summary>
    /// <typeparam name="T">子类类型。</typeparam>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()
    {
        protected static T _instance = default(T);
        private bool _isReleasing;

        public static T Instance
        {
            get
            {
                if (null == _instance)
                {
                    if (SingletonSystem.IsReleasing)
                    {
                        throw new System.InvalidOperationException($"Cannot create {typeof(T).FullName} while business modules are shutting down.");
                    }
                    _instance = new T();
                    _instance.OnInit();
                    SingletonSystem.Retain(_instance);
                }

                return _instance;
            }
        }

        public static bool IsValid => _instance != null;

        protected Singleton()
        {
#if UNITY_EDITOR
            string st = new StackTrace().ToString();
            // using const string to compare simply
            if (!st.Contains("GameLogic.Singleton`1[T].get_Instance"))
            {
                UnityEngine.Debug.LogError($"请必须通过Instance方法来实例化{typeof(T).FullName}类");
            }
#endif
        }

        protected virtual void OnInit()
        {
        }

        public virtual void Active()
        {
        }

        public virtual void Release()
        {
            // 旧引用或重入调用不能再次清理，更不能释放此类型的新实例。
            if (!ReferenceEquals(_instance, this) || _isReleasing)
            {
                return;
            }

            _isReleasing = true;
            try
            {
                OnRelease();
            }
            finally
            {
                SingletonSystem.Release(this);
                _instance = null;
                _isReleasing = false;
            }
        }

        protected virtual void OnRelease()
        {
        }
    }
}
