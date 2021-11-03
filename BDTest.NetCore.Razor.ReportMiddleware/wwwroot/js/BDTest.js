function copyToClipboard(text) {
    navigator.clipboard.writeText(text);

    showSnackbar("Copied to clipboard!");
}

function isInViewport(elem) {
    let bounding = elem.getBoundingClientRect();
    return (
        bounding.top >= 0 &&
        bounding.left >= 0 &&
        bounding.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        bounding.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

function getRandomColor() {
    let letters = '0123456789ABCDEF'.split('');
    let color = '#';
    for (let i = 0; i < 6; i++) {
        color += letters[Math.floor(Math.random() * 16)];
    }
    return color;
}

function getUrlWithAppendedParameter(paramName, paramValue) {
    let url = window.location.href;
    if (url.indexOf(paramName + "=") >= 0) {
        let prefix = url.substring(0, url.indexOf(paramName));
        let suffix = url.substring(url.indexOf(paramName));
        suffix = suffix.substring(suffix.indexOf("=") + 1);
        suffix = (suffix.indexOf("&") >= 0) ? suffix.substring(suffix.indexOf("&")) : "";
        url = prefix + paramName + "=" + paramValue + suffix;
    } else {
        if (url.indexOf("?") < 0)
            url += "?" + paramName + "=" + paramValue;
        else
            url += "&" + paramName + "=" + paramValue;
    }

    return url;
}

function showSnackbar(text) {
    // Get the snackbar DIV
    let snackbar = document.getElementById("snackbar");

    snackbar.textContent = text;

    // Add the "show" class to DIV
    snackbar.classList.add("show");

    // After 3 seconds, remove the show class from DIV
    setTimeout(function() {
        snackbar.classList.remove("show");
    }, 3000);
}

function toggleAlternativeText(element) {
    if (element.hasAttribute("toggle-alternative-text")) {
        let alternativeText = element.getAttribute("toggle-alternative-text");
        let currentText = element.textContent;

        element.textContent = alternativeText;
        element.setAttribute("toggle-alternative-text", currentText);
    }
}

function getAll(selector) {
    let parent = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : document;

    return Array.prototype.slice.call(parent.querySelectorAll(selector), 0);
}

function setCookie(name, value, days) {
    var expires = "";
    if (days) {
        var date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/";
}

function getCookie(name) {
    var nameEQ = name + "=";
    var ca = document.cookie.split(';');
    for (var i = 0; i < ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) == ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
}

onDomLoaded(function () {
    let dateRangePicker = document.getElementById("dateRangePicker");
    
    if(dateRangePicker == null) {
        return
    }
    
    dateRangePicker.addEventListener("click", selectDateRanges); 
});

onDomLoaded(function () {
    let headerCheckbox = document.getElementById("checkbox-header");
    
    if(headerCheckbox == null) {
        return
    }
    
    headerCheckbox.addEventListener('click', function() {
        if(headerCheckbox.checked === false) {
            for (let checkbox of document.getElementsByClassName("checkbox-child")) {
                checkbox.checked = false;
            }
        } else {
            for (let checkbox of document.getElementsByClassName("checkbox-child")) {
                if(!checkbox.parentElement.parentElement.classList.contains("invisible")) {
                    checkbox.checked = true;
                }
            }
        }
    }) 
});

function selectDateRanges() {
    alert("Select the start date and time range");
    var startDatePicker = new SimplePicker("#startdatetimepickercontainer", {compactMode:true});
    startDatePicker.open()

    startDatePicker.on('submit', function(startDate, readableStartDate){
        alert("Now select the end date and time range");
        var endDatePicker = new SimplePicker("#enddatetimepickercontainer", {compactMode:true});
        endDatePicker.open()

        endDatePicker.on('submit', function(endDate, readableEndDate){
            window.location.href = getUrlWithAppendedParameter("datetimerange", `${startDate.toISOString()}..${endDate.toISOString()}`)
        })
    })
}

function post(path, params, method='post') {

    // The rest of this code assumes you are not using a library.
    // It can be made less verbose if you use one.
    const form = document.createElement('form');
    form.method = method;
    form.action = path;

    for (const key in params) {
        if (params.hasOwnProperty(key)) {
            const hiddenField = document.createElement('input');
            hiddenField.type = 'hidden';
            hiddenField.name = key;
            hiddenField.value = params[key];

            form.appendChild(hiddenField);
        }
    }

    document.body.appendChild(form);
    form.submit();
}

function disableButtonAndSpin(element) {
    spinnerCursor();
    element.disabled = true;
    element.innerHTML = "<span class=\"spinner-border spinner-border-sm\" role=\"status\" aria-hidden=\"true\"></span>\n" +
        "<span class=\"visually-hidden\">Loading...</span";
}

function spinnerCursor() {
    document.body.style.cursor = 'wait';
}

const toggleActions = {
    TOGGLE: "toggle",
    COLLAPSE: "collapse",
    EXPAND: "expand"
}

function expandScenarioGroups(toggleAction) {
    let scenarioGroupRows = document.getElementsByClassName("scenario-group");
    for (let scenarioGroupRow of scenarioGroupRows) {
        expandScenarioGroup(scenarioGroupRow, toggleAction)
    }
}

function expandScenarioGroup(scenarioGroupRow, toggleAction) {
    let scenarioGroup = scenarioGroupRow.getAttribute("scenario-group");
    let childRows = document.querySelectorAll('[parent-scenario-group="' + scenarioGroup + '"]')
    for (let childRow of childRows) {
        switch (toggleAction) {
            case toggleActions.TOGGLE:
                childRow.classList.toggle("show");
                break;
            case toggleActions.EXPAND:
                childRow.classList.add("show");
                break;
            case toggleActions.COLLAPSE:
                childRow.classList.remove("show");
                break;
        }
    }
}

function setElementVisibility(element, toggleAction) {
    switch (toggleAction) {
        case toggleActions.TOGGLE:
            element.classList.toggle("invisibleElement");
            break;
        case toggleActions.EXPAND:
            element.classList.remove("invisibleElement");
            break;
        case toggleActions.COLLAPSE:
            element.classList.add("invisibleElement");
            break;
    }
}

function toggleElementVisibility(element) {
    setElementVisibility(element, toggleActions.TOGGLE);
}

function showElement(element) {
    setElementVisibility(element, toggleActions.EXPAND);
}

function hideElement(element) {
    setElementVisibility(element, toggleActions.COLLAPSE);
}