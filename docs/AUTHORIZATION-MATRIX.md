# AUTHORIZATION MATRIX

## 1. Role Definitions

1. **Anonymous / Public**: Unauthenticated visitor. Limited strictly to login page and public health probes (`/health/live`).
2. **Division User**: Operational user assigned to a specific NWKRTC division (Belagavi, Hubballi-Dharwad, Gadag, Uttara Kannada, Haveri, Bagalkot, Vijayapura, Chikkodi). Access is scoped strictly to their assigned division.
3. **Central Office (CO)**: Central administration legal officers (DivisionID = 0 or 5). Possesses cross-division visibility and case management authority.
4. **Chief Law Officer (CLO)**: Senior legal executive. Authorized to approve/reject appeal proposals, sanction settlements, and formulate legal opinions.
5. **Managing Director (MD)**: Executive leadership. Final authority for statutory appeals, High Court / Supreme Court filing sanctions, and significant financial settlements.
6. **System Administrator (Admin)**: Full system access, user management, and master data administration.

---

## 2. Module Permissions Matrix

| Module / Action | Anonymous | Division User | Central Office (CO) | Chief Law Officer (CLO) | Managing Director (MD) | Administrator |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Authentication & Profile** |
| View Login Screen | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW |
| Login / Authenticate | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW |
| Password Change (Self) | DENY | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW |
| **MVC / MACT Cases** |
| View Cases | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Create Case | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Edit Case | DENY | Own Div Only* | All Divisions | All Divisions | All Divisions | All Divisions |
| Forward to Central Office | DENY | Own Div Only | ALLOW | ALLOW | ALLOW | ALLOW |
| **Labour Cases & Arising Applications** |
| View Labour Cases | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Create Labour Case | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Edit Labour Case | DENY | Own Div Only* | All Divisions | All Divisions | All Divisions | All Divisions |
| Record Reinstatement | DENY | Own Div Only | ALLOW | ALLOW | ALLOW | ALLOW |
| Record CO Action | DENY | DENY | ALLOW | ALLOW | ALLOW | ALLOW |
| **Gratuity Management** |
| View Gratuity Cases | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Create Gratuity Case | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Edit Gratuity Case | DENY | Own Div Only* | All Divisions | All Divisions | All Divisions | All Divisions |
| Record CLO Action | DENY | DENY | DENY | ALLOW | ALLOW | ALLOW |
| Record MD Approval | DENY | DENY | DENY | DENY | ALLOW | ALLOW |
| **Appeals & Execution Petitions (EP)** |
| View / Manage Appeals | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| Appeal Action (CLO) | DENY | DENY | DENY | ALLOW | ALLOW | ALLOW |
| Manage Execution Petitions | DENY | Own Div Only | All Divisions | All Divisions | All Divisions | All Divisions |
| **Judgments & Documents** |
| View / Search Judgments | DENY | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW |
| Upload Landmark Judgment | DENY | DENY | ALLOW | ALLOW | ALLOW | ALLOW |
| Download Court Order PDF | DENY | ALLOW | ALLOW | ALLOW | ALLOW | ALLOW |
| **System Administration** |
| User Management | DENY | DENY | DENY | DENY | DENY | ALLOW |
| Master Data (Divisions, Courts) | DENY | DENY | DENY | DENY | DENY | ALLOW |
| View Audit Logs | DENY | DENY | DENY | ALLOW | ALLOW | ALLOW |

\* *Note*: Once forwarded to Central Office (`SentToCO == true`), division users are placed in read-only lock status to preserve legal document integrity.
