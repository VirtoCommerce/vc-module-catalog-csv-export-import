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
                // A cleared ui-select leaves the model undefined/empty; normalize it to null so the
                // field is explicitly unmapped (and not silently dropped when serialized).
                angular.forEach(blade.importConfiguration.propertyMaps, function (mappingItem) {
                    if (!mappingItem.csvColumnName) {
                        mappingItem.csvColumnName = null;
                    }
                    if (mappingItem.customValue === '' || mappingItem.customValue === undefined) {
                        mappingItem.customValue = null;
                    }
                });

                if (angular.isFunction(blade.onSave)) {
                    blade.onSave(blade.importConfiguration);
                }

                $scope.bladeClose();
            };

            blade.isLoading = false;
        }]);
