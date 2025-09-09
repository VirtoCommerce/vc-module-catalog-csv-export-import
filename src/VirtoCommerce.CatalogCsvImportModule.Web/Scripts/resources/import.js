angular.module('virtoCommerce.catalogCsvImportModule')
    .factory('virtoCommerce.catalogCsvImportModule.import', ['$resource', function ($resource) {

        return $resource('api/catalogcsvimport/import', {}, {
            getMappingConfiguration: { method: 'GET', url: 'api/catalogcsvimport/import/mappingconfiguration', isArray: false },
            run: { method: 'POST', url: 'api/catalogcsvimport/import', isArray: false }
        });
    }]);
