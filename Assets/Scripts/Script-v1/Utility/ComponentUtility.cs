using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility
{
    /// <summary>
    /// Utility class dan Extension Methods untuk mempermudah pengambilan, validasi,
    /// caching, dan pencarian referensi Component di Unity (GameObject, Transform, Hierarchy, Scene).
    /// Mengurangi boilerplate code serta mencegah NullReferenceException dan lag akibat GC.
    /// </summary>
    public static class ComponentUtility
    {
        #region Caching / Lazy Get Helper

        /// <summary>
        /// Mengambil referensi component dan menyimpannya ke backing field jika masih null.
        /// Sangat efisien untuk pattern cached property / lazy initialization di Awake/Start/Update.
        /// Contoh:
        /// <code>
        /// private Animator _anim;
        /// public Animator Anim => ComponentUtility.GetOrCache(ref _anim, this);
        /// </code>
        /// </summary>
        public static T GetOrCache<T>(ref T backingField, Component source) where T : class
        {
            if (backingField == null && source != null)
            {
                backingField = source.GetComponent<T>();
            }
            return backingField;
        }

        /// <summary>
        /// Mengambil referensi component dan menyimpannya ke backing field jika masih null (pada GameObject).
        /// </summary>
        public static T GetOrCache<T>(ref T backingField, GameObject source) where T : class
        {
            if (backingField == null && source != null)
            {
                backingField = source.GetComponent<T>();
            }
            return backingField;
        }

        #endregion

        #region Extension Methods: Get or Add

        /// <summary>
        /// Mengambil component tipe T jika ada, atau otomatis menambahkan (AddComponent) jika belum ada.
        /// Contoh:
        /// <code>
        /// Rigidbody2D rb = gameObject.GetOrAddComponent&lt;Rigidbody2D&gt;();
        /// </code>
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null) return null;
            if (!gameObject.TryGetComponent<T>(out var component))
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// Mengambil component tipe T jika ada, atau otomatis menambahkan (AddComponent) jika belum ada.
        /// </summary>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            if (component == null) return null;
            return component.gameObject.GetOrAddComponent<T>();
        }

        #endregion

        #region Hierarchy Search: Children & Parents

        /// <summary>
        /// Mencari child transform berdasarkan nama (bisa recursive di seluruh sub-children).
        /// Contoh:
        /// <code>
        /// Transform iconTf = transform.FindDeepChild("IconImage");
        /// </code>
        /// </summary>
        public static Transform FindDeepChild(this Transform parent, string childName, StringComparison comparison = StringComparison.Ordinal)
        {
            if (parent == null) return null;

            // 1. Cek direct children terlebih dahulu (cepat)
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, childName, comparison))
                {
                    return child;
                }
            }

            // 2. Rekursif ke sub-children
            foreach (Transform child in parent)
            {
                var result = child.FindDeepChild(childName, comparison);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Mencari child berdasarkan nama secara deep, lalu mengambil component tipe T.
        /// Sangat cocok untuk runtime canvas/HUD builder atau auto discovery references.
        /// Contoh:
        /// <code>
        /// TextMeshProUGUI txt = panelTransform.GetComponentInChild&lt;TextMeshProUGUI&gt;("TimerText");
        /// </code>
        /// </summary>
        public static T GetComponentInChild<T>(this Transform parent, string childName) where T : Component
        {
            Transform child = parent.FindDeepChild(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        /// <summary>
        /// Mencari child berdasarkan nama secara deep, lalu mengambil component tipe T.
        /// </summary>
        public static T GetComponentInChild<T>(this GameObject parent, string childName) where T : Component
        {
            if (parent == null) return null;
            return parent.transform.GetComponentInChild<T>(childName);
        }

        #endregion

        #region Safe Component Retrieval & Logging

        /// <summary>
        /// Mengambil component dengan TryGetComponent, namun jika tidak ditemukan akan mengeluarkan
        /// pesan error diagnostik yang jelas dan informatif di Unity Console.
        /// </summary>
        public static bool TryGetRequiredComponent<T>(this Component source, out T component, string customContext = "") where T : Component
        {
            component = null;
            if (source == null) return false;

            if (source.TryGetComponent<T>(out component))
            {
                return true;
            }

            string ctx = string.IsNullOrEmpty(customContext) ? "" : $" [{customContext}]";
            Debug.LogError($"<color=red>[ComponentUtility]</color>{ctx} GameObject '{source.name}' membutuhkan component bertipe '{typeof(T).Name}', namun tidak ditemukan!", source);
            return false;
        }

        /// <summary>
        /// Mengambil component dengan TryGetComponent pada GameObject, dengan logging jika gagal.
        /// </summary>
        public static bool TryGetRequiredComponent<T>(this GameObject gameObject, out T component, string customContext = "") where T : Component
        {
            component = null;
            if (gameObject == null) return false;

            if (gameObject.TryGetComponent<T>(out component))
            {
                return true;
            }

            string ctx = string.IsNullOrEmpty(customContext) ? "" : $" [{customContext}]";
            Debug.LogError($"<color=red>[ComponentUtility]</color>{ctx} GameObject '{gameObject.name}' membutuhkan component bertipe '{typeof(T).Name}', namun tidak ditemukan!", gameObject);
            return false;
        }

        #endregion

        #region Scene / Global Find Helpers

        /// <summary>
        /// Menemukan component singleton / instance di scene secara aman dan modern.
        /// Mendukung Unity 2023+ (FindAnyObjectByType) dengan fallback ke FindObjectOfType.
        /// </summary>
        public static T FindInScene<T>(bool includeInactive = false) where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var findOptions = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            return UnityEngine.Object.FindAnyObjectByType<T>(findOptions);
#else
            return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#endif
        }

        /// <summary>
        /// Menemukan semua component di scene secara aman dan modern.
        /// </summary>
        public static T[] FindAllInScene<T>(bool includeInactive = false) where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var findOptions = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            return UnityEngine.Object.FindObjectsByType<T>(findOptions, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

        #endregion
    }
}
