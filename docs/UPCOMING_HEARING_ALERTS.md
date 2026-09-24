# 📅 Upcoming Hearing Alerts & Notification Specifications

### 1. Upcoming Hearing Alerts (Across All Modules)
These alerts ensure advocates and division managers never miss a court date.

- **"Upcoming Hearing Tomorrow"**: Triggered 1 day before the `NextHearingDate` for MVC, Labour, or Other Courts.
- **"Hearing Reminder (3 Days)"**: Triggered 3 days before a hearing, giving staff time to prepare files or contact the advocate.
- **"Missing Hearing Date"**: Alert sent if a case is marked as "Pending" but the `NextHearingDate` has passed and hasn't been updated in the system.

### 2. Compliance & Payment Deadlines
- **"Gratuity Compliance Due"**: Alert triggered 7 days before the deadline to deposit the Gratuity Amount ordered by the Controlling Authority.
- **"MVC Award Deposit Deadline"**: Alert triggered when an MVC judgment is passed against the Corporation, reminding the division to deposit the compensation amount before the 30/60 day legal deadline.
- **"Labour Reinstatement/Backwages Due"**: Reminders to comply with a Labour Court order regarding an employee's reinstatement.

### 3. Appeal Windows & Judgments
- **"Appeal Window Expiring (15 Days Left)"**: If an MVC or Labour case was lost, the system tracks the `DateOfOrder` and alerts the Central Office that the 90-day window to file an MFA (High Court Appeal) or Writ Petition is closing soon.
- **"Judgment Copy Pending"**: If a case is marked as "Disposed" but no Judgment PDF has been uploaded after 14 days, it alerts the user to procure the certified copy.

### 4. Stay Orders & Execution Petitions (Urgent Alerts)
- **"Interim Stay Expiring"**: For Appeals/Labour cases, if a Stay was granted until a specific date, the system warns users 5 days before it expires so they can apply for an extension.
- **"New Execution Petition (EP) Filed"**: Alerting the division immediately when an EP is registered against them.

### 5. System & Workflow Notifications
- **"New Case Assigned"**: If the Central Office registers an Appeal (MFA/SLP) belonging to a specific Depot/Division, the Division User gets an alert.
- **"Advocate Missing"**: An alert if a case has an upcoming hearing but no Advocate has been assigned.
