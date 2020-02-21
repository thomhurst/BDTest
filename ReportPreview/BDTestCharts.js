google.charts.load('current', {'packages':['corechart']});
      google.charts.setOnLoadCallback(drawChart);

      function drawChart() {

        var datastatus0 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Passed', 16], ['Failed', 2], ['Inconclusive', 0], ['Not Implemented', 1]
        ]);

        var optionsstatus0 = {
          title: 'Test Status', width: 700, height: 400, pieSliceText: 'none', slices: {
            0: { color: '#34A853' },
            1: { color: '#EA4335' },
 2: { color: '#FBBc05' },
 3: { color: '#4285F4' }          }
        };

        var chartstatus0 = new google.visualization.PieChart(document.getElementById('piechartstatus0'));

        chartstatus0.draw(datastatus0, optionsstatus0);
      var datastatus1 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Passed', 12], ['Failed', 2], ['Inconclusive', 0], ['Not Implemented', 1]
        ]);

        var optionsstatus1 = {
          title: 'Test Status', width: 700, height: 400, pieSliceText: 'none', slices: {
            0: { color: '#34A853' },
            1: { color: '#EA4335' },
 2: { color: '#FBBc05' },
 3: { color: '#4285F4' }          }
        };

        var chartstatus1 = new google.visualization.PieChart(document.getElementById('piechartstatus1'));

        chartstatus1.draw(datastatus1, optionsstatus1);
      var datastatus2 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Passed', 1], ['Failed', 0], ['Inconclusive', 0], ['Not Implemented', 0]
        ]);

        var optionsstatus2 = {
          title: 'Test Status', width: 700, height: 400, pieSliceText: 'none', slices: {
            0: { color: '#34A853' },
            1: { color: '#EA4335' },
 2: { color: '#FBBc05' },
 3: { color: '#4285F4' }          }
        };

        var chartstatus2 = new google.visualization.PieChart(document.getElementById('piechartstatus2'));

        chartstatus2.draw(datastatus2, optionsstatus2);
      var datastatus3 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Passed', 3], ['Failed', 0], ['Inconclusive', 0], ['Not Implemented', 0]
        ]);

        var optionsstatus3 = {
          title: 'Test Status', width: 700, height: 400, pieSliceText: 'none', slices: {
            0: { color: '#34A853' },
            1: { color: '#EA4335' },
 2: { color: '#FBBc05' },
 3: { color: '#4285F4' }          }
        };

        var chartstatus3 = new google.visualization.PieChart(document.getElementById('piechartstatus3'));

        chartstatus3.draw(datastatus3, optionsstatus3);
      var datatime0 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Asynchronous Scenario Failure', 607672],['With step text list', 9978],['Exception test', 69469],['Func step text test', 18661],['Test 1', 250183],['Test 3', 230230],['With step text', 30281],['Not implemented test', 50221],['Custom Scenario', 19950],['Func step text reverser test', 39892],['Test 4', 29899],['Test', 158564],['Test 4', 190352],['Test 5', 29535],['Constant arguments', 41705],['Asynchronous Scenario', 9978],['Test 1', 3767719],['Dynamic arguments', 29922],['Test 2', 220262]
        ]);

        var optionstime0 = {
          title: 'Test Times', width: 700, height: 400, pieSliceText: 'none'};

        var charttime0 = new google.visualization.PieChart(document.getElementById('piecharttime0'));

        charttime0.draw(datatime0, optionstime0);
      var datatime1 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Asynchronous Scenario Failure', 607672],['With step text list', 9978],['Exception test', 69469],['Func step text test', 18661],['Test 1', 250183],['Test 3', 230230],['With step text', 30281],['Not implemented test', 50221],['Custom Scenario', 19950],['Func step text reverser test', 39892],['Test 4', 29899],['Test 4', 190352],['Test 5', 29535],['Asynchronous Scenario', 9978],['Test 2', 220262]
        ]);

        var optionstime1 = {
          title: 'Test Times', width: 700, height: 400, pieSliceText: 'none'};

        var charttime1 = new google.visualization.PieChart(document.getElementById('piecharttime1'));

        charttime1.draw(datatime1, optionstime1);
      var datatime2 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Test', 158564]
        ]);

        var optionstime2 = {
          title: 'Test Times', width: 700, height: 400, pieSliceText: 'none'};

        var charttime2 = new google.visualization.PieChart(document.getElementById('piecharttime2'));

        charttime2.draw(datatime2, optionstime2);
      var datatime3 = google.visualization.arrayToDataTable([
          ['Scenarios', 'Amount'],
          ['Constant arguments', 41705],['Test 1', 3767719],['Dynamic arguments', 29922]
        ]);

        var optionstime3 = {
          title: 'Test Times', width: 700, height: 400, pieSliceText: 'none'};

        var charttime3 = new google.visualization.PieChart(document.getElementById('piecharttime3'));

        charttime3.draw(datatime3, optionstime3);
      }