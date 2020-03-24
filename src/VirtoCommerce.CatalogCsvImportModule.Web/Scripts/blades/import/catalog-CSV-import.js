﻿angular.module('virtoCommerce.catalogCsvImportModule')
.controller('virtoCommerce.catalogCsvImportModule.catalogCSVimportController', ['$scope', 'platformWebApp.bladeNavigationService', 'virtoCommerce.catalogCsvImportModule.import', 'platformWebApp.notifications',
    function ($scope, bladeNavigationService, importResourse, notificationsResource) {

        var blade = $scope.blade;
        blade.isLoading = false;
        blade.title = 'catalogCsvImportModule.blades.catalog-CSV-import.title';

        $scope.$on("new-notification-event", function (event, notification) {
            if (blade.notification && notification.id == blade.notification.id) {
                angular.copy(notification, blade.notification);
            }
        });

        $scope.setForm = function (form) {
            $scope.formScope = form;
        }

        $scope.bladeHeadIco = 'fa fa-file-archive-o';


    }]);
