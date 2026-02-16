using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DockYardHUD : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public LayerMask dockYardMask;

    [Header("TMP")]
    public TMP_Text titleText;
    public TMP_Text statusText;

    [Header("Selected Target (Runtime)")]
    public DockYard target;

    [Header("Refresh")]
    public float refreshInterval = 0.2f;
    private float _timer;

    private void Reset()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                TryPickDockYard();
        }

        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            Refresh();
        }
    }

    private void TryPickDockYard()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 500f, dockYardMask))
        {
            var dy = hit.collider.GetComponentInParent<DockYard>();
            if (dy != null) target = dy;
        }
    }

    public void SetSelectedTarget(DockYard yard)
    {
        target = yard;
        Refresh();
    }

    private void Refresh()
    {
        if (titleText == null || statusText == null) return;

        if (target == null)
        {
            titleText.text = "DockYard: (none)";
            statusText.text = "Click a DockYard to view status.\n(T/Y for test enqueue)";
            return;
        }

        titleText.text = $"DockYard: {target.name}";
        statusText.text =
            $"Queued: {target.QueuedCount}\n" +
            $"Active: {target.ActiveCount}/{target.DockCapacity}";
    }
}
