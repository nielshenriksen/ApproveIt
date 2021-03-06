﻿namespace Create.Plugin.ApproveIt.EventHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using Newtonsoft.Json;
    using Pocos;
    using Umbraco.Core;
    using Umbraco.Core.Events;
    using Umbraco.Core.Models;
    using Umbraco.Core.Models.Membership;
    using Umbraco.Core.Persistence;
    using Umbraco.Core.Publishing;
    using Umbraco.Core.Services;
    using Umbraco.Web;

    /// <summary>
    /// Application Events Handler.
    /// </summary>
    public class ApproveItEventsHandler : ApplicationEventHandler
    {
        /// <summary>
        /// Event that runs when the application is first started.
        /// </summary>
        /// <param name="umbracoApplication">The umbraco application.</param>
        /// <param name="applicationContext">The application context.</param>
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            // Get the Umbraco Database context
            var ctx = applicationContext.DatabaseContext;

            DatabaseSchemaHelper db = new DatabaseSchemaHelper(ctx.Database, applicationContext.ProfilingLogger.Logger, ctx.SqlSyntax);

            // Check if the DB table does NOT exist
            if (!db.TableExist(Settings.APPROVE_IT_CHANGE_HISTORY_TABLE))
            {
                // Create DB table - and set overwrite to false
                db.CreateTable<ChangeHistory>(false);
            }

            ContentService.SendingToPublish += SendingToPublishEventHandler;
        }

        /// <summary>
        /// Sendings to publish event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="SendToPublishEventArgs{IContent}"/> instance containing the event data.</param>
        private void SendingToPublishEventHandler(IContentService sender, SendToPublishEventArgs<IContent> args)
        {
            if (args == null || args.Entity == null || args.Entity.Properties == null || args.Entity.Properties.Count == 0)
            {
                return;
            }

            // Get user making changes
            IUser user = UmbracoContext.Current.Security.CurrentUser;

            // Fetch original content
            IContent originalContent = UmbracoContext.Current.Application.Services.ContentService.GetById(args.Entity.Id);

            // Get the Umbraco db
            var db = UmbracoContext.Current.Application.DatabaseContext.Database;

            // Start a transaction
            using (var scope = db.GetTransaction())
            {
                DateTime now = DateTime.Now;

                // Find which properties were actually changed
                IList<Property> dirtyProps = args.Entity.Properties.ToList();
                foreach (Property dirtyProp in dirtyProps)
                {
                    Property originalProp = originalContent.Properties
                        .Where(x => x.Id == dirtyProp.Id)
                        .FirstOrDefault();

                    if (originalProp != null &&
                        originalProp.Value != null &&
                        string.Compare(originalProp.Value.ToString(), dirtyProp.Value.ToString(), false) != 0)
                    {
                        // Creates a new change history table item
                        ChangeHistory newChange = new ChangeHistory();

                        newChange.PropertyId = dirtyProp.Id;
                        newChange.PropertyAlias = dirtyProp.Alias;
                        newChange.NodeId = args.Entity.Id;
                        newChange.UpdateDate = now;
                        newChange.UpdatedBy = user.Username;
                        newChange.PreviousValue = originalProp.Value.ToString();
                        newChange.CurrentValue = dirtyProp.Value.ToString();
                        newChange.PropertyTypeAlias = dirtyProp.PropertyType.PropertyEditorAlias;                   

                        db.Insert(newChange);
                    }
                }

                // Commit the transaction
                scope.Complete();
            }
        }
    }
}