﻿using FakeXrmEasy;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xrm.Oss.XTL.Templating;

namespace Xrm.Oss.XTL.Interpreter.Tests
{
    [TestFixture]
    public class RecordTableTests
    {
        private void SetupContext(XrmFakedContext context)
        {
            context.AddExecutionMock<RetrieveEntityRequest>(req =>
            {
                var entityMetadata = new EntityMetadata();

                var property = entityMetadata
                    .GetType()
                    .GetProperty("Attributes");

                var subjectLabel = new StringAttributeMetadata
                {
                    LogicalName = "subject",
                    DisplayName = new Label
                    {
                        UserLocalizedLabel = new LocalizedLabel
                        {
                            LanguageCode = 1033,
                            Label = "Subject Label"
                        }
                    }
                };

                var descriptionLabel = new StringAttributeMetadata
                {
                    LogicalName = "description",
                    DisplayName = new Label
                    {
                        UserLocalizedLabel = new LocalizedLabel
                        {
                            LanguageCode = 1033,
                            Label = "Description Label"
                        }
                    }
                };

                var attributes = new AttributeMetadata[] { subjectLabel, descriptionLabel };
                property.GetSetMethod(true).Invoke(entityMetadata, new object[] { attributes });

                return new RetrieveEntityResponse
                {
                    Results = new ParameterCollection
                    {
                        { "EntityMetadata", entityMetadata }
                    }
                };
            });
        }

        [Test]
        public void It_Should_Not_Fail_On_Empty_Table()
        {
            var context = new XrmFakedContext();
            var service = context.GetFakedOrganizationService();
            var tracing = context.GetFakeTracingService();

            var contact = new Entity
            {
                LogicalName = "contact",
                Id = Guid.NewGuid(),
                Attributes =
                {
                    { "firstname", "Frodo" }
                }
            };

            SetupContext(context);
            context.Initialize(new Entity[] { contact });

            var formula = "RecordTable(Fetch(\"<fetch no-lock='true'><entity name='task'><attribute name='description' /><attribute name='subject' /><filter><condition attribute='regardingobjectid' operator='eq' value='{0}' /></filter></entity></fetch>\"), \"task\", false, \"subject\", \"description\")";

            var expected = @"<table>
<tr><th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Subject Label</th>
<th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Description Label</th>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 1</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 1</td>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 2</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 2</td>
<tr />
</table>".Replace("\r", "").Replace("\n", "");

            Assert.That(() => new XTLInterpreter(formula, contact, null, service, tracing).Produce(), Throws.Nothing);
        }

        [Test]
        public void It_Should_Create_Sub_Record_Table_Without_Url()
        {
            var context = new XrmFakedContext();
            var service = context.GetFakedOrganizationService();
            var tracing = context.GetFakeTracingService();

            var contact = new Entity
            {
                LogicalName = "contact",
                Id = Guid.NewGuid(),
                Attributes =
                {
                    { "firstname", "Frodo" }
                }
            };

            var task = new Entity
            {
                LogicalName = "task",
                Id = Guid.NewGuid(),
                Attributes =
                {
                    { "subject", "Task 1" },
                    { "description", "Description 1" },
                    { "regardingobjectid", contact.ToEntityReference() }
                }
            };

            var task2 = new Entity
            {
                LogicalName = "task",
                Id = Guid.NewGuid(),
                Attributes =
                {
                    { "subject", "Task 2" },
                    { "description", "Description 2" },
                    { "regardingobjectid", contact.ToEntityReference() }
                }
            };

            SetupContext(context);
            context.Initialize(new Entity[] { contact, task, task2 });

            var formula = "RecordTable(Fetch(\"<fetch no-lock='true'><entity name='task'><attribute name='description' /><attribute name='subject' /><filter><condition attribute='regardingobjectid' operator='eq' value='{0}' /></filter></entity></fetch>\"), \"task\", false, \"subject\", \"description\")";

            var expected = @"<table>
<tr><th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Subject Label</th>
<th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Description Label</th>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 1</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 1</td>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 2</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 2</td>
<tr />
</table>".Replace("\r", "").Replace("\n", "");

            var result = new XTLInterpreter(formula, contact, null, service, tracing).Produce();

            Assert.That(result.Replace("\r", "").Replace("\n", ""), Is.EqualTo(expected));
        }

        [Test]
        public void It_Should_Create_Sub_Record_Table_With_Url()
        {
            var context = new XrmFakedContext();
            var service = context.GetFakedOrganizationService();
            var tracing = context.GetFakeTracingService();

            var contact = new Entity
            {
                LogicalName = "contact",
                Id = Guid.NewGuid(),
                Attributes =
                {
                    { "firstname", "Frodo" }
                }
            };

            var task = new Entity
            {
                LogicalName = "task",
                Id = new Guid("76f167d6-35b3-44ae-b2a0-9373dee13e82"),
                Attributes =
                {
                    { "subject", "Task 1" },
                    { "description", "Description 1" },
                    { "regardingobjectid", contact.ToEntityReference() }
                }
            };

            var task2 = new Entity
            {
                LogicalName = "task",
                Id = new Guid("5c0370f2-9b79-4abc-86d6-09260d5bbfed"),
                Attributes =
                {
                    { "subject", "Task 2" },
                    { "description", "Description 2" },
                    { "regardingobjectid", contact.ToEntityReference() }
                }
            };

            SetupContext(context);
            context.Initialize(new Entity[] { contact, task, task2 });

            var formula = "RecordTable(Fetch(\"<fetch no-lock='true'><entity name='task'><attribute name='description' /><attribute name='subject' /><filter><condition attribute='regardingobjectid' operator='eq' value='{0}' /></filter></entity></fetch>\"), \"task\", true, \"subject\", \"description\")";

            var expected = @"<table>
<tr><th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Subject Label</th>
<th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">Description Label</th>
<th style=""border:1px solid black;text-align:left;padding:1px 15px 1px 5px"">URL</th>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 1</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 1</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px""><a href=""https://test.local/main.aspx?etn=task&id=76f167d6-35b3-44ae-b2a0-9373dee13e82&newWindow=true&pagetype=entityrecord"">https://test.local/main.aspx?etn=task&id=76f167d6-35b3-44ae-b2a0-9373dee13e82&newWindow=true&pagetype=entityrecord</a></td>
<tr />
<tr>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Task 2</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px"">Description 2</td>
<td style=""border:1px solid black;padding:1px 15px 1px 5px""><a href=""https://test.local/main.aspx?etn=task&id=5c0370f2-9b79-4abc-86d6-09260d5bbfed&newWindow=true&pagetype=entityrecord"">https://test.local/main.aspx?etn=task&id=5c0370f2-9b79-4abc-86d6-09260d5bbfed&newWindow=true&pagetype=entityrecord</a></td>
<tr />
</table>".Replace("\r", "").Replace("\n", "");

            var result = new XTLInterpreter(formula, contact, new OrganizationConfig { OrganizationUrl = "https://test.local" }, service, tracing).Produce();

            Assert.That(result.Replace("\r", "").Replace("\n", ""), Is.EqualTo(expected));
        }
    }
}
