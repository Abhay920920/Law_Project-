# 🎯 MVC/MACT Case Management System - Quick Start Guide

## ✅ What's Been Created

### Database Scripts (Ready to Execute)
1. **`migration_scripts/01_complete_database_schema.sql`**
   - Creates `MVCCaseDB` database with UTF-8 collation
   - 8 tables with relationships and indexes
   
2. **`migration_scripts/02_seed_data.sql`**
   - 12 Divisions with Kannada names
   - 76 Depots across all divisions
   - Default admin user and roles

### ASP.NET MVC Application (Ready to Run)
- **Complete folder structure** with Controllers, Models, Views, DAL
- **Authentication system** with login/logout
- **Dashboard** with navigation
- **Modern UI** with Bootstrap 5 and gradient design
- **Database connection** configured for SQL Server

---

## 🚀 Steps to Get Started

### 1️⃣ Setup Database (5 minutes)

```powershell
# Navigate to migration scripts folder
cd "C:\Users\adts-\Desktop\Law Project\migration_scripts"

# Create database and tables
sqlcmd -S localhost -i "01_complete_database_schema.sql"

# Import seed data (12 divisions, 76 depots)
sqlcmd -S localhost -d MVCCaseDB -i "02_seed_data.sql"

# Verify (optional)
sqlcmd -S localhost -d MVCCaseDB -Q "SELECT COUNT(*) FROM DIVISION_MASTER"
```

**Expected:** Should return 12 divisions

---

### 2️⃣ Configure Application (2 minutes)

1. Open `MVCCaseManagement/appsettings.json`
2. Update connection string if needed:
   ```json
   "MVCCaseDB": "Server=localhost;Database=MVCCaseDB;Integrated Security=true;TrustServerCertificate=true;"
   ```
   - Change `localhost` to your SQL Server name if different
   - Use `(localdb)\MSSQLLocalDB` for LocalDB

---

### 3️⃣ Run Application (2 minutes)

```powershell
# Navigate to project folder
cd "C:\Users\adts-\Desktop\Law Project\MVCCaseManagement"

# Restore packages
dotnet restore

# Build project
dotnet build

# Run application
dotnet run
```

**Open browser:** `https://localhost:5001`

---

### 4️⃣ Login

**Username:** `admin`  
**Password:** `Admin@123`

---

## 📁 Project Files Created

### Configuration Files (3)
- `MVCCaseManagement.csproj` - Project file
- `Program.cs` - Application startup
- `appsettings.json` - Configuration

### Models (3)
- `Models/User.cs` - User and LoginViewModel
- `Models/MVCCase.cs` - MVC case model
- `Models/Masters.cs` - Division, Depot, MACT, Status

### Data Access Layer (2)
- `DAL/DBHelper.cs` - Database connection helper
- `DAL/UserRepository.cs` - User data operations

### Controllers (2)
- `Controllers/AccountController.cs` - Login/logout
- `Controllers/DashboardController.cs` - Dashboard

### Views (4)
- `Views/_ViewStart.cshtml` - View configuration
- `Views/_ViewImports.cshtml` - Global imports
- `Views/Account/Login.cshtml` - Login page
- `Views/Dashboard/Index.cshtml` - Dashboard home
- `Views/Shared/_Layout.cshtml` - Main layout with sidebar

### Common Utilities (1)
- `Common/PasswordHelper.cs` - Password hashing & session keys

### Documentation (2)
- `README.md` - Full documentation
- `QUICKSTART.md` - This file

**Total: 18 files created**

---

## 🎨 What You'll See

### Login Page
- Modern gradient design (purple theme)
- Username/password fields
- Remember me checkbox
- Responsive layout

### Dashboard
- Left sidebar with navigation
- Statistics cards (Total, Pending, Disposed)
- Quick action buttons
- User info in header

### Navigation Menu
- Dashboard
- MVC Cases (future)
- New Case (future)
- Reports (future)
- Masters (admin only, future)
- Logout

---

## ✅ Completed Features

| Module | Status | Details |
|--------|--------|---------|
| Database Schema | ✅ Complete | 8 tables, indexes, constraints |
| Seed Data | ✅ Complete | 12 divisions, 76 depots, admin user |
| Authentication | ✅ Complete | Login, logout, session management |
| Dashboard | ✅ Complete | Layout, navigation, placeholders |
| Password Security | ✅ Complete | SHA-256 hashing |
| Role-Based UI | ✅ Complete | Dynamic menu based on role |

---

## 🔜 Next Development Steps

### Phase 2: Case Management (Next)
1. Create **New Case Registration** form
   - Division dropdown (from DIVISION_MASTER)
   - MACT dropdown (from MACT_MASTER)
   - MVC No, Year, Notice Date
   - Validation: MVC No + Year uniqueness

2. Create **Case Listing** page
   - DataTable with pagination
   - Search by MVC No
   - Filter by Division, Status, Year
   - Edit button for each case

3. Create **Case Edit** page
   - Update case details
   - Change status
   - Add disposal date

### Phase 3: Reports
- Pending cases report with filters
- Disposed cases report
- Excel export using EPPlus

### Phase 4: Masters (Admin Only)
- Division CRUD
- MACT CRUD
- User management

---

## 🐛 Common Issues & Solutions

### ❌ "Cannot connect to database"
**Solution:**
```powershell
# Check SQL Server is running
sqlcmd -S localhost -Q "SELECT @@VERSION"

# If using LocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
```

### ❌ "Login failed for admin"
**Solution:**
Re-run seed data script:
```powershell
sqlcmd -S localhost -d MVCCaseDB -i "02_seed_data.sql"
```

### ❌ "Port 5001 already in use"
**Solution:**
```powershell
dotnet run --urls "https://localhost:7001"
```

---

## 📊 Database Overview

```
MVCCaseDB
├── ROLE_MASTER (2 roles)
├── DIVISION_MASTER (12 divisions)
│   ├── BGK - Bagalkot
│   ├── BGM - Belagavi
│   ├── CKD - Chikkodi
│   └── ... (9 more)
├── DEPOT_MASTER (76 depots)
├── MACT_MASTER (empty - to be populated by admin)
├── CASE_STATUS_MASTER (4 statuses)
├── USERS (1 admin user)
├── MVC_CASE (empty - for user data)
└── AUDIT_LOG (optional)
```

---

## 🎓 Development Tips

1. **Adding New Pages:**
   - Create Controller in `Controllers/`
   - Create View in `Views/ControllerName/`
   - Use `_Layout.cshtml` for consistent UI

2. **Database Operations:**
   - Use `DBHelper` for queries
   - Always use parameterized queries
   - Create repositories in `DAL/`

3. **Session Access:**
   ```csharp
   var userId = HttpContext.Session.GetInt32(SessionKeys.UserID);
   var role = HttpContext.Session.GetString(SessionKeys.RoleName);
   ```

4. **Master Data Dropdowns:**
   ```csharp
   // In Controller
   var divisions = _masterRepository.GetAllDivisions();
   ViewBag.Divisions = new SelectList(divisions, "DivisionID", "DivisionNameEnglish");
   
   // In View
   @Html.DropDownListFor(m => m.DivisionID, (SelectList)ViewBag.Divisions)
   ```

---

## 📞 Need Help?

1. Check main [`README.md`](./README.md) for detailed documentation
2. Review [`implementation_plan.md`](../migration_scripts/../brain/.../implementation_plan.md) for architecture
3. Verify SRS requirements document
4. Check SQL scripts in `migration_scripts/`

---

**🎉 Your system is ready! Start with database setup, then run the application.**
