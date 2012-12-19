﻿/// <reference path="/Scripts/AjaxHelper.js" />

$(function () {
    var rowTemplate = _.template($("#row-template").text());
    var $table = $('#absences-table');
    var currentYear = ViewData.currentYear;
    var year = ViewData.year;

    var columns = [
            DataTablesHelpers.column.link(
                DataTablesHelpers.column.fullName(
                    function (data) { return data.employee.LastName; },
                    function (data) { return data.employee.FirstName; }),
                function (data) { return urlGenerator.action("Edit", "Employees", data.employee.Id); },
                {
                    sClass: "cell-name"
                })
            //TODO: Other columns with related abscence date, like employee summary, or something
    ];
    
    var current = moment([year]);
    var end = moment([year]).endOf("year").startOf('day').valueOf();
    
    while (current.valueOf() <= end) {
        var weekday = current.day();
        columns.push({
            bSortable: false,
            sClass: "cell-day" + (weekday == 0 || weekday == 6 ? " weekend" : ""), //TODO: puedo setear las clases acá en lugar de en fnCreatedRow?
            mData: function () {
                //TODO: day absence data
                return "";
            }
        });
        current.add('days', 1);
    }

    var table = $table.dataTable(
    {
        bPaginate: false,
        bAutoWidth: false,
        aoColumns: columns,
        sDom: "<'row-fluid'<'span6'T><'span6'f>r>t<'row-fluid'<'span6'i><'span6'p>>",
        oTableTools: {
            aButtons: [
                {
                    sExtends: "print",
                    sButtonText: "Imprimir"
                },
                {
                    sExtends: "copy",
                    sButtonText: "Copiar"
                },
                {
                    sExtends: "pdf",
                    sButtonText: "PDF"
                },
                {
                    sExtends: "csv",
                    sButtonText: "Excel"
                }
            ]
        },
        fnCreatedRow: function (nRow, aData, iDataIndex) {
            //console.log("fnCreatedRow");
            var $row = $(nRow);
            for (var i in aData.employee && aData.employee && aData.employee.Absences) {
                var absence = aData.employee.Absences[i];
                var from = moment(absence.RealDate).startOf('day');
                var to = moment(absence.To || absence.RealDate).startOf('day');
                if (to.valueOf() < from.valueOf)
                    to = from;

                if (to.year() >= ViewData.year && from.year() <= ViewData.year) 
                {
                    var end = to.valueOf();
                    var current = from;
                    while (current.valueOf() <= end) {
                        $td = $row.find("td.cell-day:eq(" + current.format("DDD") + ")");
                        var tdAbsences = $td.data("absences");
                        if (!tdAbsences) {
                            tdAbsences = [];
                            $td.data("absences", tdAbsences);
                        }
                        tdAbsences.push(absence);
                        $td.addClass("absence " + absence.AbsenceType + " " + absence.ReasonSlug);
                        current.add('days', 1);
                    }
                }
            }
        },
    });
    
    var processor = new AjaxHelper.BunchProcessor(
        function (take, skip, callback) {
            jQuery.getJSON(urlGenerator.action("AbsenceBunch", "Absences"), { year: year, Skip: skip, Take: take, Term: "mos" }, function (data, textStatus, jqXHR) {
                callback(data);
            });
        },
        function (data, take, skip) {
            //  le.log(data);
            $table.dataTable().fnAddData(
                _.map(data.Items, function (employee) {
                    //console.log(employee);
                    //Prepare absence data
                    return {
                        employee: employee
                    };
                }));
        },
        function (data, take, skip) {
            return data.TotalResults - skip - take;
        },
        function () {
            //console.log("td.absence");
            //console.log($("td.absence"));
        }
    );
    processor.run(ViewData.bsize);
});