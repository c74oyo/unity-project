using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual transport job display item for ActiveTaskPanelUI.
/// Shows: route name, cargo summary, trip progress, progress bar, ETA, vehicle info.
/// Data source: MultiTripTransportJob (groups multiple single-trip orders).
/// </summary>
public class TransportJobItemUI : MonoBehaviour
{
    [Header("Route Info")]
    [Tooltip("Route name or source → destination")]
    public TextMeshProUGUI routeNameText;

    [Tooltip("Cargo summary (e.g. '500x iron_ore')")]
    public TextMeshProUGUI cargoText;

    [Header("Progress")]
    [Tooltip("Trip progress text (e.g. 'Trip 2/5')")]
    public TextMeshProUGUI tripProgressText;

    [Tooltip("Progress bar (0..1 based on tripsCompleted / totalTripsNeeded)")]
    public Slider progressSlider;

    [Tooltip("Progress percentage text (optional)")]
    public TextMeshProUGUI progressPercentText;

    [Tooltip("Progress bar fill image (color changes by progress)")]
    public Image progressFillImage;

    [Header("Time")]
    [Tooltip("Estimated remaining time for entire job")]
    public TextMeshProUGUI etaText;

    [Header("Vehicle Info")]
    [Tooltip("Vehicle count text (e.g. '3 vehicles, 2 in transit')")]
    public TextMeshProUGUI vehicleText;

    [Header("State Indicators")]
    [Tooltip("Quest link icon — shown when job is linked to a quest")]
    public GameObject questLinkIcon;

    [Tooltip("Background image (for alternate row coloring)")]
    public Image backgroundImage;

    // ============ Setup ============

    /// <summary>
    /// Populate this item with a MultiTripTransportJob's data.
    /// </summary>
    public void Setup(MultiTripTransportJob job, TradeRoute route)
    {
        if (job == null) return;

        // ---- Route name ----
        if (routeNameText != null)
        {
            string name = null;

            if (route != null && !string.IsNullOrEmpty(route.displayName))
            {
                name = route.displayName;
            }
            else
            {
                // Fallback: try to get outpost display name
                string target = job.targetOutpostId ?? "???";
                if (NPCManager.Instance != null)
                {
                    var outpost = NPCManager.Instance.GetOutpost(job.targetOutpostId);
                    if (outpost != null)
                        target = outpost.displayName;
                }
                name = $"→ {target}";
            }

            routeNameText.text = name;
        }

        // ---- Cargo summary ----
        if (cargoText != null)
        {
            if (job.totalCargo != null && job.totalCargo.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var cargo in job.totalCargo)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    string resName = FormatResourceName(cargo.resourceId);
                    sb.Append($"{cargo.amount}x {resName}");
                }
                cargoText.text = sb.ToString();
            }
            else
            {
                cargoText.text = "No cargo";
            }
        }

        // ---- Trip progress ----
        if (tripProgressText != null)
            tripProgressText.text = $"Trip {job.tripsCompleted}/{job.totalTripsNeeded}";

        // ---- Progress bar ----
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = job.Progress;
        }

        // ---- Progress percentage ----
        if (progressPercentText != null)
            progressPercentText.text = $"{job.Progress:P0}";

        // ---- Progress bar color (blue → green gradient) ----
        if (progressFillImage != null)
        {
            progressFillImage.color = Color.Lerp(
                new Color(0.2f, 0.6f, 1f),   // blue (start)
                new Color(0.2f, 0.9f, 0.3f),  // green (done)
                job.Progress
            );
        }

        // ---- ETA ----
        if (etaText != null)
        {
            float eta = EstimateJobETA(job, route);
            if (eta > 0f && eta < float.MaxValue)
            {
                etaText.text = $"~{FormatTime(eta)}";
            }
            else if (job.IsComplete)
            {
                etaText.text = "Done";
            }
            else
            {
                etaText.text = "--:--";
            }
        }

        // ---- Vehicle info ----
        if (vehicleText != null)
        {
            vehicleText.text = $"{job.assignedVehicles} vehicle(s), {job.vehiclesInTransit} in transit";
        }

        // ---- Quest link icon ----
        if (questLinkIcon != null)
            questLinkIcon.SetActive(!string.IsNullOrEmpty(job.questInstanceId));
    }

    /// <summary>
    /// Set alternate row background color.
    /// </summary>
    public void SetAlternateBackground(bool alternate)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = alternate
                ? new Color(1f, 1f, 1f, 0.03f)
                : new Color(0f, 0f, 0f, 0f);
        }
    }

    // ============ ETA Calculation ============

    /// <summary>
    /// Estimate remaining time for the entire job.
    /// Formula: ceil(remainingTrips / assignedVehicles) * (travelTime + returnTime)
    /// </summary>
    private float EstimateJobETA(MultiTripTransportJob job, TradeRoute route)
    {
        if (job == null || job.IsComplete) return 0f;
        if (TradeManager.Instance == null || route == null) return -1f;

        float travelTime = TradeManager.Instance.CalculateTravelTime(route);
        if (travelTime >= float.MaxValue) return float.MaxValue;

        float returnTime = TradeManager.Instance.CalculateRoadTravelTime(route);
        if (returnTime >= float.MaxValue) returnTime = travelTime;

        float fullRoundTrip = travelTime + returnTime;

        int remainingTrips = job.totalTripsNeeded - job.tripsCompleted;
        if (remainingTrips <= 0) return 0f;

        int vehicles = Mathf.Max(1, job.assignedVehicles);

        // Parallel batches: ceil(remainingTrips / vehicles) full round trips
        int batchesRemaining = Mathf.CeilToInt((float)remainingTrips / vehicles);
        return Mathf.Max(0f, batchesRemaining * fullRoundTrip);
    }

    // ============ Formatting Helpers ============

    private static string FormatTime(float seconds)
    {
        if (seconds <= 0f) return "0:00";
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }

    private static string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "Unknown";
        // Use MarketGoodsItemUI's formatter if available
        return MarketGoodsItemUI.FormatResourceName(resourceId);
    }
}
