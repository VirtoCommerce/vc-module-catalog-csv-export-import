angular.module('virtoCommerce.catalogCsvImportModule')
    .controller('virtoCommerce.catalogCsvImportModule.catalogCSVimportWizardMappingStepController', [
        '$scope', 'platformWebApp.bladeNavigationService',
        function ($scope, bladeNavigationService) {
            var blade = $scope.blade;

            $scope.clearPropertyCsvColumns = function () {
                blade.importConfiguration.propertyCsvColumns.length = 0;
            };

            $scope.setForm = function (form) {
                $scope.formScope = form;
            };

            $scope.isValid = function () {
                return $scope.formScope && $scope.formScope.$valid;
            };

            $scope.saveChanges = function () {
                $scope.bladeClose();
            };

            blade.isLoading = false;
        }]);
