using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class ButtonSetupTool : MonoBehaviour
{
    [MenuItem("Tools/Dashboard/Setup All Nav Buttons")]
    public static void SetupAllNavButtons()
    {
        // Find the Sidebar in the scene
        Transform sidebar = GameObject.Find("Sidebar")?.transform;
        
        if (sidebar == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find 'Sidebar' GameObject in the scene", "OK");
            return;
        }

        int count = 0;

        // Get the ButtonFeedback type dynamically
        System.Type buttonFeedbackType = System.Type.GetType("ButtonFeedback");
        
        if (buttonFeedbackType == null)
        {
            EditorUtility.DisplayDialog("Error", "ButtonFeedback script not found. Make sure it's imported in the project.", "OK");
            return;
        }

        // Iterate through all children (nav items)
        foreach (Transform child in sidebar)
        {
            // Find the Button component in this nav item
            Button button = child.GetComponent<Button>();
            
            if (button == null)
            {
                button = child.GetComponentInChildren<Button>();
            }

            if (button != null)
            {
                // Add ButtonFeedback using reflection if it doesn't exist
                var feedback = button.GetComponent(buttonFeedbackType);
                if (feedback == null)
                {
                    feedback = button.gameObject.AddComponent(buttonFeedbackType);
                }

                // Set colors via reflection
                SetPrivateField(feedback, "normalColor", new Color(0.08f, 0.21f, 0.57f, 1f));    // #0A3591
                SetPrivateField(feedback, "hoverColor", new Color(0.15f, 0.35f, 0.75f, 1f));     // #1E5BC4
                SetPrivateField(feedback, "pressedColor", new Color(0.05f, 0.12f, 0.40f, 1f));   // #051F40
                SetPrivateField(feedback, "selectedColor", new Color(0.25f, 0.50f, 0.90f, 1f));  // #3A7FD4
                SetPrivateField(feedback, "transitionSpeed", 8f);

                count++;
                EditorUtility.SetDirty(button.gameObject);
            }
        }

        if (count > 0)
        {
            EditorUtility.DisplayDialog("Success", $"Applied ButtonFeedback to {count} buttons!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "No buttons found in Sidebar", "OK");
        }

        EditorUtility.SetDirty(sidebar.gameObject);
        AssetDatabase.SaveAssets();
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }
}

