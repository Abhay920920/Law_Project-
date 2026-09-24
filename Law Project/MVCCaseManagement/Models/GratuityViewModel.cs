using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace MVCCaseManagement.Models
{
    public class GratuityViewModel : GratuityCase
    {
        public List<SelectListItem> DivisionList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CourtList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> StatusList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> WorkingStatusList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> GratuityCourtList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> GratuityAdvocateList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> DesignationList { get; set; } = new List<SelectListItem>();
        public IFormFile? AdverseJudgmentFile { get; set; }
        public IFormFile? AppealJudgmentFile { get; set; }
        
        // Mirrored UI Properties
        
        public IFormFile? InitialActionFile1 { get; set; }
        // Path properties inherited from GratuityCase
        public IFormFile? InitialActionFile2 { get; set; }
        // Path properties inherited from GratuityCase

        public IFormFile? StayOrderFile1 { get; set; }
        public IFormFile? StayOrderFile2 { get; set; }


    }
}
