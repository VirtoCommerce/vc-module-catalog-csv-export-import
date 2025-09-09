angular.module('virtoCommerce.catalogCsvImportModule')
    .factory('virtoCommerce.catalogCsvImportModule.WebApi', ['$resource', function ($resource) {
        return $resource('api/catalogcsvimport');
    }]);
