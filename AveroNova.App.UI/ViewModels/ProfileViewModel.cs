using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.App.UI.ViewModels
{
    public class ProfileViewModel
    {
        // Success
        public string SuccessTitle { get; set; } =
            "Profile completed successfully!";

        public string SuccessMessage { get; set; } =
            "Your account is now active. You can start using AveroNova ERP.";


        // Company Information
        public string CompanyName { get; set; } =
            "AveroNova ERP";

        public string OwnerName { get; set; } =
            "Faizan Ali";

        public string GstNumber { get; set; } =
            "27ABCDE1234F1Z5";

        public string MobileNumber { get; set; } =
            "+91 98765 43210";

        public string Email { get; set; } =
            "faizanali@gmail.com";

        public string Address { get; set; } =
            "123 Business Park, MG Road, Mumbai";

        public string State { get; set; } =
            "Maharashtra";

        public string City { get; set; } =
            "Mumbai";

        public string Pincode { get; set; } =
            "400001";


        // Admin Account
        public string Username { get; set; } =
            "faizanali";

        public string Password { get; set; } =
            "••••••••";

        public string ConfirmPassword { get; set; } =
            "••••••••";
    }
}
