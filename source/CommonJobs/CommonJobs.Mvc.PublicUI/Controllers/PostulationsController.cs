﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CommonJobs.Domain;
using CommonJobs.Application.AttachmentStorage;
using CommonJobs.Application.SharedLinks;
using CommonJobs.Infrastructure.Mvc;
using CommonJobs.Infrastructure.RavenDb;
using NLog;
using CommonJobs.Utilities;
using CommonJobs.Application.Suggestions;

namespace CommonJobs.Mvc.PublicUI.Controllers
{
    public class PostulationsController : CommonJobsController
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        private TemporalFileReference SaveTemporalFile(HttpPostedFileBase file)
        {
            var temporalFolderPath = Configuration.TemporalUploadsPath;
            if (!Directory.Exists(temporalFolderPath))
            {
                Directory.CreateDirectory(temporalFolderPath);
            }
            var internalFileName = Guid.NewGuid().ToString();
            var temporalFullName = Path.Combine(temporalFolderPath, internalFileName);
            file.SaveAs(temporalFullName);
            return new TemporalFileReference() 
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                InternalFileName = internalFileName
            };
        }

        private void GenerateApplicant(Postulation postulation)
        {
            //TODO: move it to an Action
            var applicant = new Applicant()
            {
                JobSearchId = postulation.JobSearchId,
                Email = postulation.Email,
                FirstName = postulation.FirstName,
                LastName = postulation.LastName,
                TechnicalSkills = postulation.TechnicalSkills,
                LinkedInLink = postulation.LinkedInUrl
            };
            RavenSession.Store(applicant);

            applicant.Notes = new List<NoteWithAttachment>();

            if (postulation.Curriculum != null)
            {
                var curriculum = GenerateAttachment(applicant, postulation.Curriculum);
                applicant.AddGeneralNote("Currículum", curriculum);
            }

            if (!string.IsNullOrEmpty(postulation.Comment))
            {
                applicant.AddGeneralNote("Nota de postulación:\n\n" + postulation.Comment);
            }
        }

        private void DeleteTemporalAttachment(TemporalFileReference temporalReference)
        {
            var temporalFolderPath = Configuration.TemporalUploadsPath;
            var temporalFilePath = Path.Combine(temporalFolderPath, temporalReference.InternalFileName);
            System.IO.File.Delete(temporalFilePath);
        }

        private AttachmentReference GenerateAttachment(object entity, TemporalFileReference temporalReference)
        {
            AttachmentReference result;
            var temporalFolderPath = Configuration.TemporalUploadsPath;
            var temporalFilePath = Path.Combine(temporalFolderPath, temporalReference.InternalFileName);
            using (var stream = System.IO.File.OpenRead(temporalFilePath))
            {
                result = ExecuteCommand(new SaveAttachment(
                    entity,
                    temporalReference.OriginalFileName,
                    stream));
            }
            return result;
        }

        private void PrepareJobSearchView(JobSearch jobSearch)
        {
            var md = new MarkdownDeep.Markdown();
            ViewBag.JobSearchId = jobSearch.Id;
            ViewBag.Title = jobSearch.Title;
            ViewBag.PublicNotes = new MvcHtmlString(md.Transform(jobSearch.PublicNotes));
            ViewBag.TechnicalSkillLevels = TechnicalSkillLevelExtensions.GetValues();
        }

        private ActionResult NotFoundOrNotAvailable()
        {
            Response.StatusCode = 404;
            Response.StatusDescription = "Not found or not available";
            return View("NotFound");
        }

        private ActionResult InternalError()
        {
            Response.StatusCode = 500;
            Response.StatusDescription = "Internal error";
            return View("Error");
        }

        public ActionResult Create(long jobSearchNumber, string slug = null)
        {
            var jobSearch = RavenSession.Load<JobSearch>(jobSearchNumber);
            if (jobSearch == null || !jobSearch.IsPublic)
                return NotFoundOrNotAvailable();

            PrepareJobSearchView(jobSearch);
            return View();
        }

        public ActionResult TechnicalSkillSuggestions(string term, int maxSuggestions = 8)
        {
            var results = Query(new GetSuggestions(x => x.TechnicalSkillName, term, maxSuggestions));
            return Json(results.Select(x => new { id = x, label = x }));
        }

        [HttpPost]
        public ActionResult Create(Postulation postulation, HttpPostedFileBase curriculumFile)
        {
            JobSearch jobSearch = null;
            try
            {
                jobSearch = RavenSession.Load<JobSearch>(postulation.JobSearchId);
            }
            catch (Exception ex)
            {
                log.ErrorException("Error loading job search", ex);
                return InternalError();
            }

            if (jobSearch == null || !jobSearch.IsPublic)
                return NotFoundOrNotAvailable();

            if (curriculumFile == null)
                ModelState.AddModelError("curriculumFile", "Requerido");
            
            if (ModelState.IsValid)
            {
                try
                {
                    postulation.Curriculum = SaveTemporalFile(curriculumFile);
                }
                catch (Exception e)
                {
                    log.ErrorException("Error uploading file", e);
                    log.Dump(LogLevel.Error, postulation);
                    return InternalError();
                }

                try
                {
                    GenerateApplicant(postulation);
                    DeleteTemporalAttachment(postulation.Curriculum);

                    return RedirectToAction("Thanks", "Postulations");
                }
                catch (Exception e)
                {
                    log.ErrorException("Unexpected error creating postulations", e);
                    log.Dump(LogLevel.Error, postulation);
                    return InternalError();
                }
            }

            PrepareJobSearchView(jobSearch);
            return View();
        }

        public ActionResult Thanks()
        {
            return View("Thanks");
        }
    }
}
