angular.module('virtoCommerce.catalogCsvImportModule')
    .controller('virtoCommerce.catalogCsvImportModule.catalogCSVexportController', [
        '$scope', 'platformWebApp.bladeNavigationService', 'virtoCommerce.catalogCsvImportModule.export', 'virtoCommerce.inventoryModule.fulfillments', 'virtoCommerce.pricingModule.pricelists',
        function ($scope, bladeNavigationService, exportResourse, fulfillments, pricelists) {

            $scope.pageSize = 20;
            $scope.fulfillmentCenters = [];
            const blade = $scope.blade;
            blade.fulfilmentCenterId = undefined;
            blade.pricelistId = undefined;
            blade.isLoading = false;
            blade.title = 'catalogCsvImportModule.blades.catalog-CSV-export.title';
            blade.titleValues = { name: blade.catalog ? blade.catalog.name : '' };

            $scope.columnDelimiters = [
                { name: "catalogCsvImportModule.wizards.catalog-CSV-import.labels.comma", value: "," },
                { name: "catalogCsvImportModule.wizards.catalog-CSV-import.labels.semicolon", value: ";" },
                { name: "catalogCsvImportModule.wizards.catalog-CSV-import.labels.tab", value: "\t" },
                { name: "catalogCsvImportModule.wizards.catalog-CSV-import.labels.vertical", value: "|" }
            ];
            blade.columnDelimiter = ",";

            function initializeBlade() {
            }

            $scope.fetchfulfillmentCenters = ($select) => {
                $select.page = 0;
                $scope.fulfillmentCenters = [];

                $scope.fetchNextfulfillmentCenters($select).then(() => {
                    if (!blade.fulfilmentCenterId) {
                        blade.fulfilmentCenterId = _.first($scope.fulfillmentCenters).id;
                    }
                });
            };

            $scope.fetchNextfulfillmentCenters = ($select) => {
                let criteria = {
                    SearchPhrase: $select.search,
                    take: $scope.pageSize,
                    skip: $select.page * $scope.pageSize
                }

                return fulfillments.search(criteria, data => {
                    $scope.fulfillmentCenters = $scope.fulfillmentCenters.concat(data.results);
                    $select.page++;
                }).$promise;
            };

            $scope.$on("new-notification-event", function (event, notification) {
                if (blade.notification && notification.id === blade.notification.id) {
                    angular.copy(notification, blade.notification);
                }
            });

            $scope.startExport = function () {
                exportResourse.getMappingConfiguration({ delimiter: encodeURIComponent(blade.columnDelimiter) },
                    function (exportConfiguration) {
                        exportResourse.run({
                            catalogId: blade.catalog.id,
                            categoryIds: _.map(blade.selectedCategories, function (x) { return x.id }),
                            productIds: _.map(blade.selectedProducts, function (x) { return x.id }),
                            fulfilmentCenterId: blade.fulfilmentCenterId,
                            pricelistId: blade.pricelistId,
                            configuration: exportConfiguration,
                        },
                            function (data) { blade.notification = data; },
                            function (error) { bladeNavigationService.setError('Error ' + error.status, $scope.blade); });
                    }, function (error) {
                        bladeNavigationService.setError('Error ' + error.status, blade);
                    });
            };

            $scope.setForm = function (form) {
                $scope.formScope = form;
            };

            $scope.blade.fetchPricelists = function (criteria) {
                return pricelists.search(criteria);
            };

            $scope.blade.headIcon = 'fa fa-file-archive-o';

            initializeBlade();
        }]);
