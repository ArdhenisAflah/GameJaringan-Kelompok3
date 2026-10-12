#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TruckOrder.Editor
{
    public static class TruckOrderEditorSetup
    {
        [MenuItem("Tools/Setup Truck Order System")]
        public static void SetupTruckOrderSystem()
        {
            // 1. Setup OrderSystem
            OrderSystem orderSystem = Object.FindObjectOfType<OrderSystem>();
            if (orderSystem == null)
            {
                GameObject osObj = new GameObject("OrderSystem");
                orderSystem = osObj.AddComponent<OrderSystem>();
                Undo.RegisterCreatedObjectUndo(osObj, "Create OrderSystem");
            }

            // 2. Setup TruckQueueManager pada 'Arah Jalan Mobil Pickup'
            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            if (roadParent != null)
            {
                TruckQueueManager queueManager = roadParent.GetComponent<TruckQueueManager>();
                if (queueManager == null)
                {
                    queueManager = Undo.AddComponent<TruckQueueManager>(roadParent);
                }
                queueManager.ResolveWaypointsIfNeeded();
                EditorUtility.SetDirty(roadParent);
            }
            else
            {
                Debug.LogWarning("[TruckOrderSetup] GameObject 'Arah Jalan Mobil Pickup' tidak ditemukan di scene!");
            }

            // 3. Setup Canvas UI (TruckOrderBubbleUI)
            TruckOrderBubbleUI.CreateRuntimeCanvasUI();

            // 4. Setup TruckInteraction & Disable Legacy SpriteRenderer pada 'Order Box'
            GameObject orderBoxObj = GameObject.Find("Order Box");
            if (orderBoxObj != null)
            {
                var legacySr = orderBoxObj.GetComponent<SpriteRenderer>();
                if (legacySr != null && legacySr.enabled)
                {
                    legacySr.enabled = false;
                    EditorUtility.SetDirty(orderBoxObj);
                }

                TruckInteraction interaction = orderBoxObj.GetComponent<TruckInteraction>();
                if (interaction == null)
                {
                    interaction = Undo.AddComponent<TruckInteraction>(orderBoxObj);
                    EditorUtility.SetDirty(orderBoxObj);
                }
            }
            else if (Object.FindObjectOfType<TruckInteraction>() == null)
            {
                GameObject interactionObj = new GameObject("TruckInteraction");
                interactionObj.transform.position = new Vector3(5.35f, -2.13f, 0f);
                interactionObj.AddComponent<TruckInteraction>();
                Undo.RegisterCreatedObjectUndo(interactionObj, "Create TruckInteraction");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );

            Debug.Log("<color=green>[TruckOrderSetup]</color> Sukses menyiapkan Truck & Order System di scene aktif!");
        }
    }
}
#endif
