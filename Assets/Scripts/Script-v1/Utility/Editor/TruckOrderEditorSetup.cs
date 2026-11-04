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
            // Pastikan kita bekerja pada scene Gameplay jika terbuka
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // 1. Group Headers
            GameObject managersHeader = GameObject.Find("--- MANAGERS ---");
            GameObject spawnerHeader = GameObject.Find("--- SPAWNER ---");

            // 2. Setup OrderSystem di bawah '--- MANAGERS ---'
            OrderSystem orderSystem = Object.FindObjectOfType<OrderSystem>();
            if (orderSystem == null)
            {
                GameObject osObj = new GameObject("OrderSystem");
                if (managersHeader != null)
                {
                    osObj.transform.SetParent(managersHeader.transform, false);
                }
                orderSystem = osObj.AddComponent<OrderSystem>();
                Undo.RegisterCreatedObjectUndo(osObj, "Create OrderSystem");
            }
            else if (managersHeader != null && orderSystem.transform.parent != managersHeader.transform)
            {
                orderSystem.transform.SetParent(managersHeader.transform, true);
            }

            // 3. Setup TruckQueueManager pada 'Arah Jalan Mobil Pickup' (di bawah --- SPAWNER ---)
            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            if (roadParent != null)
            {
                if (spawnerHeader != null && roadParent.transform.parent != spawnerHeader.transform)
                {
                    roadParent.transform.SetParent(spawnerHeader.transform, true);
                }

                TruckQueueManager queueManager = roadParent.GetComponent<TruckQueueManager>();
                if (queueManager == null)
                {
                    queueManager = Undo.AddComponent<TruckQueueManager>(roadParent);
                }
                queueManager.ResolveWaypointsIfNeeded();

                // Setup container [Trucks_Container] di bawah Arah Jalan Mobil Pickup atau --- SPAWNER ---
                Transform containerTrans = roadParent.transform.Find("[Trucks_Container]");
                if (containerTrans == null)
                {
                    GameObject containerGo = new GameObject("[Trucks_Container]");
                    containerGo.transform.SetParent(roadParent.transform, false);
                    containerTrans = containerGo.transform;
                    Undo.RegisterCreatedObjectUndo(containerGo, "Create Trucks_Container");
                }

                // Assign truckContainer via SerializedObject
                SerializedObject soQM = new SerializedObject(queueManager);
                SerializedProperty propContainer = soQM.FindProperty("truckContainer");
                if (propContainer != null)
                {
                    propContainer.objectReferenceValue = containerTrans;
                    soQM.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(roadParent);
            }
            else
            {
                Debug.LogWarning("[TruckOrderSetup] GameObject 'Arah Jalan Mobil Pickup' tidak ditemukan di scene!");
            }

            // 4. Setup TruckOrderCanvas UI
            TruckOrderBubbleUI ui = TruckOrderBubbleUI.CreateRuntimeCanvasUI();
            if (ui != null)
            {
                // Optional: Kelompokkan di bawah category header jika diinginkan, atau biarkan di root Canvas
                EditorUtility.SetDirty(ui.gameObject);
            }

            // 5. Setup TruckInteraction & Disable Legacy SpriteRenderer pada 'Order Box' (di bawah --- SPAWNER ---)
            GameObject orderBoxObj = GameObject.Find("Order Box");
            if (orderBoxObj != null)
            {
                if (spawnerHeader != null && orderBoxObj.transform.parent != spawnerHeader.transform)
                {
                    orderBoxObj.transform.SetParent(spawnerHeader.transform, true);
                }

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
                if (spawnerHeader != null)
                {
                    interactionObj.transform.SetParent(spawnerHeader.transform, false);
                }
                interactionObj.AddComponent<TruckInteraction>();
                Undo.RegisterCreatedObjectUndo(interactionObj, "Create TruckInteraction");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("<color=green>[TruckOrderSetup]</color> Sukses menata & menyiapkan Truck & Order System di hierarki scene Gameplay!");
        }
    }
}
#endif
