"use strict";

$(function () {
    var nav = $("#menu");
    var navTop = nav.offset().top;

    $(window).scroll(function (e) {
        var winTop = $(this).scrollTop();

        if (winTop >= navTop) {
            nav.addClass("fixed");
        } else if (winTop <= navTop) {
            nav.removeClass("fixed");
        }
    });
});
