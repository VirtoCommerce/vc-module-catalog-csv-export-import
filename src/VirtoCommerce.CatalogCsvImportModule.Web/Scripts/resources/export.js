angular.module('virtoCommerce.catalogCsvImportModule')
    .factory('virtoCommerce.catalogCsvImportModule.export', ['$resource', function ($resource) {

        return $resource('api/catalogcsvimport/export/:id', { id: '@id' }, {
            getMappingConfiguration: { method: 'GET', url: 'api/catalogcsvimport/export/mappingconfiguration', isArray: false },
            run: { method: 'POST', url: 'api/catalogcsvimport/export', isArray: false }
        });

    }]);
