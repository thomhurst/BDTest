using System.Collections.Generic;
using System.Linq;
using BDTest.Maps;
using BDTest.Test;
using Humanizer;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BDTest.ReportGenerator.PDF
{
    public class ReportDocument : IDocument
    {
        private readonly BDTestOutputModel _bdTestOutputModel;

        public ReportDocument(BDTestOutputModel bdTestOutputModel)
        {
            _bdTestOutputModel = bdTestOutputModel;
        }
        
        public DocumentMetadata GetMetadata()
        {
            return new DocumentMetadata
            {
                
            };
        }

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(50);
            
                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignRight().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
        }

        void ComposeHeader(IContainer container)
        {
            var titleTextStyle = TextStyle.Default.Size(24).Bold();
    
            container.Row(row =>
            {
                row.RelativeColumn().Stack(stack =>
                {
                    stack.Item().Text("BDTest", titleTextStyle);

                    stack.Item().Text(text =>
                    {
                        text.Span("A Testing Framework", TextStyle.Default.SemiBold());
                    });
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(20).Stack(column => 
            {
                foreach (var scenariosByStory in _bdTestOutputModel.Scenarios.GroupBy(x => x.GetStoryText()))
                {
                    column.Item().Row(row => { row.RelativeColumn().Text($"Story: {scenariosByStory.Key}"); });

                    column.Item().PaddingBottom(20).Element(x => ComposeScenarios(x, scenariosByStory.ToList()));
                }
            });
        }
        
        void ComposeScenarios(IContainer container, List<Scenario> scenarios)
        {
            var headerStyle = TextStyle.Default.SemiBold();
    
            container.Decoration(decoration =>
            {
                // header
                decoration.Header().BorderBottom(1).Padding(5).Row(row => 
                {
                    row.ConstantColumn(25).Text("#", headerStyle);
                    row.RelativeColumn(3).Text("Scenario", headerStyle);
                    row.RelativeColumn().AlignRight().Text("Status", headerStyle);
                    row.RelativeColumn().AlignRight().Text("Time", headerStyle);
                    //row.RelativeColumn().AlignRight().Text("Total", headerStyle);
                });

                // content
                decoration
                    .Content()
                    .Stack(column =>
                    {
                        foreach (var scenario in scenarios)
                        {
                            column
                                .Item()
                                .ShowEntire()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .Row(row => 
                                {
                                    row.ConstantColumn(25).Text(scenarios.IndexOf(scenario) + 1);
                                    row.RelativeColumn(3).Text(scenario.GetScenarioText());
                                    row.RelativeColumn().AlignRight().Text(scenario.Status.ToString());
                                    row.RelativeColumn().AlignRight().Text(scenario.TimeTaken.Humanize());
                                    //row.RelativeColumn().AlignRight().Text($"{item.Price * item.Quantity}$");
                                });
                        }
                    });
            });
        }
    }
}