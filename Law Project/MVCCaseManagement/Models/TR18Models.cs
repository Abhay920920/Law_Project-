using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable disable

namespace MVCCaseManagement.Models
{
    public class TR18AccidentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public AccidentDetails Data { get; set; }
    }

    public class AccidentDetails
    {
        [JsonPropertyName("accident_id")]
        public string AccidentId { get; set; }

        [JsonPropertyName("report_type")]
        public string ReportType { get; set; }

        [JsonPropertyName("date_of_accident")]
        public string AccidentDateTime { get; set; }

        [JsonPropertyName("date_of_info")]
        public string InfoDateTime { get; set; }

        [JsonPropertyName("division")]
        public string Division { get; set; }

        [JsonPropertyName("depot")]
        public string Depot { get; set; }

        [JsonPropertyName("vehicle_no")]
        public string VehicleNo { get; set; }

        [JsonPropertyName("vehicle_model")]
        public string VehicleModel { get; set; }

        [JsonPropertyName("schedule_no")]
        public string ScheduleNo { get; set; }

        [JsonPropertyName("other_vehicles")]
        public List<OtherVehicle> OtherVehicles { get; set; } = new List<OtherVehicle>();

        [JsonPropertyName("additional_nwkrtc_vehicles")]
        public List<object> AdditionalNwkrtcVehicles { get; set; } = new List<object>();

        [JsonPropertyName("accident_vehicle_type")]
        public string AccidentVehicleType { get; set; }

        [JsonPropertyName("path")]
        public string RoutePath { get; set; } // path is keyword

        [JsonPropertyName("location")]
        public string Location { get; set; } // Same as PlaceOfAccident, keeping distinct for mapping

        [JsonPropertyName("road_type")]
        public string RoadType { get; set; }

        [JsonPropertyName("driver_name")]
        public string DriverName { get; set; }

        [JsonPropertyName("driver_phone")]
        public string DriverPhone { get; set; }

        [JsonPropertyName("bille_no")]
        public string DriverBilleNo { get; set; }

        [JsonPropertyName("conductor_name")]
        public string ConductorName { get; set; }

        [JsonPropertyName("conductor_phone")]
        public string ConductorPhone { get; set; }

        [JsonPropertyName("tag_no")]
        public string ConductorTagNo { get; set; }

        [JsonPropertyName("inspecting_officer")]
        public string InspectingOfficer { get; set; }

        [JsonPropertyName("inspecting_officer_phone")]
        public string InspectingOfficerPhone { get; set; }

        [JsonPropertyName("nature_of_accident")]
        public string NatureOfAccident { get; set; }

        [JsonPropertyName("mh_number")]
        public GenderCount FatalStats { get; set; }

        [JsonPropertyName("injured_number")]
        public GenderCount InjuredStats { get; set; }

        [JsonPropertyName("other_loss")]
        public OtherLoss OtherLossStats { get; set; }

        [JsonPropertyName("person_mentioned")]
        public string PersonMentioned { get; set; }

        [JsonPropertyName("relief_details")]
        public string ReliefDetails { get; set; }

        [JsonPropertyName("hospital_name")]
        public string HospitalName { get; set; }

        [JsonPropertyName("brief_details")]
        public string BriefDetails { get; set; }

        // --- Existing Mapped Fields ---

        [JsonPropertyName("driver_condition")]
        public string DriverCondition { get; set; }

        [JsonPropertyName("conductor_condition")]
        public string ConductorCondition { get; set; }

        [JsonPropertyName("driver_format_of_recruitment")]
        public string DriverRecruitmentFormat { get; set; }

        [JsonPropertyName("conductor_format_of_recruitment")]
        public string ConductorRecruitmentFormat { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("driver_age")]
        public int? DriverAge { get; set; }

        [JsonPropertyName("conductor_age")]
        public int? ConductorAge { get; set; }

        [JsonPropertyName("driver_service")]
        public string DriverService { get; set; }

        [JsonPropertyName("conductor_service")]
        public string ConductorService { get; set; }

        [JsonPropertyName("police_station")]
        public string PoliceStation { get; set; }

        [JsonPropertyName("complaint")]
        public string Complaint { get; set; }

        [JsonPropertyName("driver_license_no")]
        public string DriverLicenseNo { get; set; }

        [JsonPropertyName("type_of_vehicle")]
        public string VehicleType { get; set; }

        [JsonPropertyName("type_of_wheeler")]
        public string TypeOfWheeler { get; set; }

        [JsonPropertyName("passenger_count")]
        public int? PassengerCount { get; set; }

        [JsonPropertyName("injured_count")]
        public int? InjuredCount { get; set; }

        [JsonPropertyName("fatal_count")]
        public int? FatalCount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("police_station_complaint")]
        public string PoliceStationComplaint { get; set; }

        [JsonPropertyName("tr18_form")]
        public TR18Form tr18_form { get; set; }

        [JsonPropertyName("images")]
        public List<MediaItem> images { get; set; } = new List<MediaItem>();

        [JsonPropertyName("videos")]
        public List<MediaItem> videos { get; set; } = new List<MediaItem>();

        // Legacy Fields (for compatibility if needed, though mostly replaced by above)
        [JsonPropertyName("fir_no")]
        public string FIRNo { get; set; }

        [JsonPropertyName("fir_date")]
        public string FIRDate { get; set; }
        
        // Capture any extra fields from API that we haven't mapped explicitly
        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new Dictionary<string, object>();

        // UI Helpers
        public List<string> PhotoUrls { get; set; } = new List<string>();
        public List<string> VideoUrls { get; set; } = new List<string>();
    }

    public class OtherVehicle
    {
        [JsonPropertyName("number")]
        public string Number { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("wheeler")]
        public string Wheeler { get; set; }
    }

    public class GenderCount
    {
        [JsonPropertyName("male")]
        public int Male { get; set; }
        [JsonPropertyName("female")]
        public int Female { get; set; }
        [JsonPropertyName("children")]
        public int Children { get; set; }
        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class OtherLoss
    {
        [JsonPropertyName("animals")]
        public List<object> Animals { get; set; } = new List<object>();
        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class TR18Form
    {
        public dynamic police_info { get; set; }
        public dynamic corp_vehicle { get; set; }
        public dynamic corp_crew { get; set; }
        public List<MediaItem> media { get; set; } = new List<MediaItem>();
    }

    public class MediaItem
    {
        [JsonPropertyName("url")]
        public string url { get; set; }

        [JsonPropertyName("type")]
        public string type { get; set; }

        [JsonPropertyName("created_at")]
        public string created_at { get; set; }
    }
}
