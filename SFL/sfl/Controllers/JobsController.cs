using sfl.Data;
using sfl.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
namespace sfl.Controllers
{
    [Authorize(Roles = "Administrator, Warehouse manager, Warehouse worker, Logistics agent, Delivery driver")]
    public class JobsController : Controller
    {
        private readonly CompanyContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string[] _navigationProperties;

        public JobsController(CompanyContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _navigationProperties = new string[] { "JobsParcels", "Staff", "JobType", "JobStatus", "JobsParcels" };
        }

        // GET: Jobs
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var currentRole = currentUser == null ? null :
                _context.UserRoles.Select(ur => new { ur.RoleId, ur.UserId })
                    .Where(ur => ur.UserId == currentUser.Id)
                    .First().RoleId;
            var currentBranch = currentUser == null ? -1 :
                _context.Staff.Select(s => new { s.Username, s.BranchID })
                    .Where(s => s.Username == currentUser.UserName)
                    .ToList()[0].BranchID;

            if (currentRole == null || currentRole == null || currentBranch == -1)
            {
                return Problem("Entity set 'CompanyContext.Jobs'  is null.");
            }

            List<Job>? view = currentRole switch
            {
                "1" => await _context.Jobs.ToListAsync(),
                "2" => await _context.Jobs.Select(j => j)
                                        .Where(j => j.Staff.BranchID == currentBranch).ToListAsync(),
                "3" => await _context.Jobs.Select(j => j)
                                        .Where(j => j.StaffUsername == currentUser.UserName).ToListAsync(),
                "4" => await _context.Jobs.Select(j => j).ToListAsync(),
                "5" => await _context.Jobs.Select(j => j)
                                        .Where(j => j.StaffUsername == currentUser.UserName).ToListAsync(),
                _ => null,
            };

            if (view != null)
            {
                for (int i = 0; i < view.Count; i++)
                {
                    view[i].JobsParcels = _context.JobsParcels
                        .Select(jp => jp)
                        .Where(jp => jp.JobID == view[i].ID)
                        .ToList();

                    view[i].JobStatus = _context.JobStatuses
                        .Select(js => js)
                        .Where(js => js.ID == view[i].JobStatusID)
                        .First();

                    view[i].JobType = _context.JobTypes
                        .Select(jt => jt)
                        .Where(jt => jt.ID == view[i].JobTypeID)
                        .First();
                }

            }

            return view != null ? View(view) : Problem("Entity set 'CompanyContext.Jobs'  is null.");
        }

        // GET: Jobs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Jobs == null)
            {
                return NotFound();
            }

            var job = await _context.Jobs
                .FirstOrDefaultAsync(m => m.ID == id);

            job.JobsParcels = _context.JobsParcels
                .Select(jp => jp)
                .Where(jp => jp.JobID == job.ID)
                .ToList();

            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }

        // GET: Jobs/Create
        [Authorize(Roles = "Administrator, Warehouse manager, Logistics agent")]
        public IActionResult Create()
        {
            var currentUserName = _userManager.GetUserName(User);
            var currentUserId = _userManager.GetUserId(User);
            var roleId = _context.UserRoles
                .Select(ur => ur)
                .Where(ur => ur.UserId == currentUserId)
                .First().RoleId;

            ViewData["Parcels"] = new SelectList(_context.Parcels, "ID", "ID");

            if (roleId == "1")
            {
                ViewData["StaffUsername"] = new SelectList(_context.Staff, "Username", "Username");
            }
            else
            {
                var currentBranch = currentUserName == null ? -1 :
                    _context.Staff.Select(s => new { s.Username, s.BranchID })
                        .Where(s => s.Username == currentUserName)
                        .First().BranchID;

                ViewData["StaffUsername"] = new SelectList(_context.Staff
                    .Select(s => s)
                    .Where(s => s.BranchID == currentBranch),
                "Username", "Username");
            }

            ViewData["JobTypeID"] = new SelectList(_context.JobTypes, "ID", "Name");
            return View();
        }

        // POST: Jobs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator, Warehouse manager, Logistics agent")]
        public async Task<IActionResult> Create([Bind("ID,StaffUsername,JobTypeID,ParcelIDs")] Job job)
        {
            var ids = Request.Form["ParcelIDs"].ToList();
            RemoveNavigationProperties();
            if (ModelState.IsValid)
            {
                job.DateCreated = DateTime.Now;
                job.JobStatusID = _context.JobStatuses.Select(j => j.ID).Where(j => j == 1).First();
                // First create job.
                _context.Add(job);
                await _context.SaveChangesAsync();

                // Create JobParcel records from selected parcels and current job.
                var parcels = new List<JobParcel>();
                foreach (var pID in Request.Form["ParcelIDs"].ToList())
                {
                    parcels.Add(new JobParcel { ParcelID = pID, JobID = job.ID });
                }

                // Link jobs and selected parcels in table JobParcel.
                _context.JobsParcels.AddRange(parcels);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            var currentUserName = _userManager.GetUserName(User);
            var currentUserId = _userManager.GetUserId(User);
            var roleId = _context.UserRoles
                .Select(ur => ur)
                .Where(ur => ur.UserId == currentUserId)
                .First().RoleId;

            if (roleId == "1")
            {
                ViewData["StaffUsername"] = new SelectList(_context.Staff, "Username", "Username");
            }
            else
            {
                var currentBranch = currentUserName == null ? -1 :
                    _context.Staff.Select(s => new { s.Username, s.BranchID })
                        .Where(s => s.Username == currentUserName)
                        .First().BranchID;

                ViewData["StaffUsername"] = new SelectList(_context.Staff
                    .Select(s => s)
                    .Where(s => s.BranchID == currentBranch),
                "Username", "Username");
            }

            ViewData["Parcels"] = new SelectList(_context.Parcels, "ID", "ID");
            ViewData["JobTypeID"] = new SelectList(_context.JobTypes, "ID", "Name");
            return View(job);
        }

        // GET: Jobs/Edit/5
        [Authorize(Roles = "Administrator, Warehouse manager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Jobs == null)
            {
                return NotFound();
            }

            ViewData[index: "JobStatusID"] = new SelectList(_context.JobStatuses, "ID", "Name");

            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
            {
                return NotFound();
            }
            return View(job);
        }

        // POST: Jobs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator, Warehouse manager")]
        public async Task<IActionResult> Edit(int id, [Bind("ID,DateCreated,DateCompleted,StaffUsername,JobStatusID,JobTypeID")] Job job)
        {
            if (id != job.ID)
            {
                return NotFound();
            }

            if (job.DateCompleted < job.DateCreated)
            {
                ViewData[index: "JobStatusID"] = new SelectList(_context.JobStatuses, "ID", "Name");
                return View(job);
            }

            RemoveNavigationProperties();
            ModelState.Remove("StaffUsername");
            if (ModelState.IsValid)
            {
                try
                {
                    if (job.JobStatusID == 2)
                    {
                        job.DateCompleted = DateTime.Now;
                    }

                    job.StaffUsername = _context.Jobs.Select(j => new { j.StaffUsername, j.ID })
                        .Where(j => j.ID == job.ID).ToList()[0].StaffUsername;
                    _context.Update(job);
                    MoveJob(job);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!JobExists(job.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData[index: "JobStatusID"] = new SelectList(_context.JobStatuses, "ID", "Name");
            return View(job);
        }

        // GET: Jobs/Delete/5
        [Authorize(Roles = "Administrator, Warehouse manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Jobs == null)
            {
                return NotFound();
            }
            var job = await _context.Jobs
                .FirstOrDefaultAsync(m => m.ID == id);
            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }

        // POST: Jobs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator, Warehouse manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Jobs == null)
            {
                return Problem("Entity set 'CompanyContext.Jobs'  is null.");
            }
            var job = await _context.Jobs.FindAsync(id);
            if (job != null)
            {
                _context.Jobs.Remove(job);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool JobExists(int id)
        {
            return (_context.Jobs?.Any(e => e.ID == id)).GetValueOrDefault();
        }

        private void RemoveNavigationProperties()
        {
            foreach (var key in _navigationProperties)
            {
                ModelState.Remove(key);
            }
        }

        private void MoveJob(Job job)
        {
            if (job == null)
            {
                return;
            }

            if (job.JobStatusID == 2)
            {
                if (job.Staff.RoleID == 3)
                {
                    if (job.JobTypeID == 3)
                    {
                        Dictionary<String, HashSet<String>> locationsToParcels = new Dictionary<String, HashSet<String>>();

                        foreach (String parcelID in job.ParcelIDs)
                        {
                            Parcel tmpParcel = _context.Parcels.Select(p => p).Where(p => p.ID == parcelID).First();

                            int recipientCode = Int32.Parse(tmpParcel.RecipientCode);
                            if ((recipientCode >= 1000 && recipientCode < 2000) || (recipientCode >= 4000 && recipientCode < 5000))
                            {
                                locationsToParcels["Skladišče LJ"].Add(parcelID);
                            }
                            else if ((recipientCode >= 2000 && recipientCode < 4000) || (recipientCode >= 9000 && recipientCode < 10000))
                            {
                                locationsToParcels["Skladišče MB"].Add(parcelID);
                            }
                            else if (recipientCode >= 5000 && recipientCode < 8000)
                            {
                                locationsToParcels["Skladišče KP"].Add(parcelID);
                            }
                            else
                            {
                                locationsToParcels["Skladišče NM"].Add(parcelID);
                            }
                        }

                        Staff tmpStaff = _context.Staff.Select(s => s).Where(s => s.Username == job.StaffUsername).First();
                        Branch tmpBranch = _context.Branches.Select(b => b).Where(b => b.ID == tmpStaff.BranchID).First();

                        foreach (string key in locationsToParcels.Keys)
                        {
                            foreach (string parcelID in locationsToParcels[key])
                            {
                                // Get random driver at warehouse with given key.
                                List<Staff> tmpStaffList = _context.Staff.Select(s => s).Where(s => s.RoleID == 4 && s.BranchID == tmpBranch.ID).ToList();
                                Staff tmpDriver = tmpStaffList[new Random().Next(0, tmpStaffList.Count)];

                                if (tmpDriver.BranchID == tmpBranch.ID)
                                {
                                    _context.Add(new Job
                                    {
                                        DateCreated = DateTime.Now,
                                        DateCompleted = null,
                                        StaffUsername = tmpDriver.Username,
                                        JobStatusID = 1,
                                        JobTypeID = 7, // Delivery cargo confirmation
                                        ParcelIDs = new List<String> { parcelID }
                                    });

                                    Parcel p = _context.Parcels.Select(p => p).Where(p => p.ID == parcelID).First();
                                    p.ParcelStatusID = 2;
                                    _context.Update(p);
                                }
                                else
                                {
                                    _context.Add(new Job
                                    {
                                        DateCreated = DateTime.Now,
                                        DateCompleted = null,
                                        StaffUsername = tmpDriver.Username,
                                        JobStatusID = 1,
                                        JobTypeID = 5, // Cargo departing confirmation
                                        ParcelIDs = new List<String> { parcelID }
                                    });
                                }
                            }
                        }
                    }
                }
                else if (job.Staff.RoleID == 5)
                {
                    if (job.JobTypeID == 5)
                    {
                        _context.Add(new Job
                        {
                            DateCreated = DateTime.Now,
                            DateCompleted = null,
                            StaffUsername = job.StaffUsername,
                            JobStatusID = 1,
                            JobTypeID = 6,
                            ParcelIDs = job.ParcelIDs
                        });
                    }
                    else if (job.JobTypeID == 7)
                    {
                        _context.Add(new Job
                        {
                            DateCreated = DateTime.Now,
                            DateCompleted = null,
                            StaffUsername = job.StaffUsername,
                            JobStatusID = 1,
                            JobTypeID = 8,
                            ParcelIDs = job.ParcelIDs
                        });

                        List<Parcel> ps = _context.Parcels.Select(p => p).Where(p => job.ParcelIDs.Contains(p.ID)).ToList();
                        foreach (Parcel p in ps)
                        {
                            p.ParcelStatusID = 3;
                            _context.Update(p);
                        }
                    }
                    else if (job.JobTypeID == 8)
                    {
                        List<Parcel> ps = _context.Parcels.Select(p => p).Where(p => job.ParcelIDs.Contains(p.ID)).ToList();
                        foreach (Parcel p in ps)
                        {
                            p.ParcelStatusID = 4;
                            _context.Update(p);
                        }
                    }
                }
            }
        }
    }
}
