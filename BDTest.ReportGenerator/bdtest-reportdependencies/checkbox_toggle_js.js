$(function () {
    const checkboxes = document.getElementsByTagName("input");
    for(var i = 0, len = checkboxes.length; i < len; i++) {
        var element = checkboxes[i];
        if(element.type === ("checkbox")) {
            element.onclick = function() {
                if(this.checked === true) {
                    $("." + this.id).show();
                } else {
                    $("." + this.id).hide();
                }
            };
        } 
    }
});