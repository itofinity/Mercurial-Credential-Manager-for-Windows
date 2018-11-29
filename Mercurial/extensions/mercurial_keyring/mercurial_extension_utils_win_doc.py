# -*- coding: utf-8 -*-
#
# mercurial extension utils: Windows doctests
#
# Copyright (c) 2015 Marcin Kasperski <Marcin.Kasperski@mekk.waw.pl>
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions
# are met:
# 1. Redistributions of source code must retain the above copyright
#    notice, this list of conditions and the following disclaimer.
# 2. Redistributions in binary form must reproduce the above copyright
#    notice, this list of conditions and the following disclaimer in the
#    documentation and/or other materials provided with the distribution.
# 3. The name of the author may not be used to endorse or promote products
#    derived from this software without specific prior written permission.
#
# THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
# IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
# OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
# IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
# INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
# NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
# DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
# THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
# THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#
# See README.txt for more details.

r'''
This module exists solely to give some examples (and doctest)
of mercurial_extension_utils behaviour on Windows. Structure
mimics that of mercurial_extension_utils.



    >>> normalize_path("~/src")
    'C:/Users/lordvader/src'
    >>> normalize_path("/some/where")
    'c:/some/where'
    >>> normalize_path("/some/where/")
    'c:/some/where'
    >>> normalize_path("../../../some/where")
    'c:/Users/lordvader/some/where'
    >>> normalize_path(r'C:\Users\Joe\source files')
    'C:/Users/Joe/source files'



    >>> belongs_to_tree("/tmp/sub/dir", "/tmp")
    'c:/tmp'
    >>> belongs_to_tree("/tmp", "/tmp")
    'c:/tmp'
    >>> belongs_to_tree("/tmp/sub", "/tmp/sub/dir/../..")
    'c:/tmp'

    >>> belongs_to_tree("/usr/sub", "/tmp")

    >>> home_work_src = os.path.join(os.environ["HOME"], "work", "src")
    >>> belongs_to_tree(home_work_src, "~/work")
    'C:/Users/lordvader/work'
    >>> belongs_to_tree("/home/lordvader/devel/webapps" if os.name != 'nt' else "c:/users/lordvader/devel/webapps",
    ...                 "~lordvader/devel")
    'C:/Users/lordvader/devel'



    >>> belongs_to_tree_group("/tmp/sub/dir", ["/bin", "/tmp"])
    'c:/tmp'
    >>> belongs_to_tree_group("/tmp", ["/tmp"])
    'c:/tmp'
    >>> belongs_to_tree_group("/tmp/sub/dir", ["/bin", "~/src"])

    >>> belongs_to_tree_group("/tmp/sub/dir", ["/tmp", "/bin", "/tmp", "/tmp/sub"])
    'c:/tmp/sub'

    >>> belongs_to_tree_group("C:/Users/lordvader/src/apps", ["~/src", "C:/Users/lordvader"])
    'C:/Users/lordvader/src'



    >>> pat = DirectoryPattern('~/src/{suffix}')
    >>> pat.is_valid()
    True
    >>> pat.search("/opt/repos/abcd")
    >>> pat.search("~/src/repos/in/tree")
    {'suffix': 'repos/in/tree'}
    >>> pat.search("c:/users/lordvader/src/repos/here")
    {'suffix': 'repos/here'}
    >>> pat.search("C:/Users/lordvader/src/repos/here")
    {'suffix': 'repos/here'}
    >>> pat.search("/home/lordvader/src")

    >>> pat = DirectoryPattern('~lordvader/devel/(item)')
    >>> pat.search("/opt/repos/abcd")
    >>> pat.search("~/devel/libxuza")
    {'item': 'libxuza'}
    >>> pat.search("~/devel/libs/libxuza")
    >>> pat.search("C:/Users/lordvader/devel/webapp")
    {'item': 'webapp'}
    >>> pat.search("/users/lordvader/devel/webapp")
    {'item': 'webapp'}
    >>> pat.search("/home/lordvader/devel")

    >>> pat = DirectoryPattern('/opt/repos/(group)/{suffix}')
    >>> pat.search("/opt/repos/abcd")
    >>> pat.search("/opt/repos/libs/abcd")
    {'group': 'libs', 'suffix': 'abcd'}
    >>> pat.search("/opt/repos/apps/mini/webby")
    {'group': 'apps', 'suffix': 'mini/webby'}

    >>> pat = DirectoryPattern('/opt/repos/(group/{suffix}')
    >>> pat.is_valid()
    False
    >>> pat.search('/opt/repos/some/where')



    >>> tf = TextFiller('{some}/text/to/{fill}')
    >>> tf.fill(some='prefix', fill='suffix')
    'prefix/text/to/suffix'
    >>> tf.fill(some='/ab/c/d', fill='x')
    '/ab/c/d/text/to/x'

    >>> tf = TextFiller('{some}/text/to/{some}')
    >>> tf.is_valid()
    True
    >>> tf.fill(some='val')
    'val/text/to/val'
    >>> tf.fill(some='ab/c/d', fill='x')
    'ab/c/d/text/to/ab/c/d'

    >>> tf = TextFiller('{prefix:_=___}/goto/{suffix:/=-}')
    >>> tf.fill(prefix='some_prefix', suffix='some/long/suffix')
    'some___prefix/goto/some-long-suffix'

    >>> tf = TextFiller('{prefix:/home/=}/docs/{suffix:.txt=.html}')
    >>> tf.fill(prefix='/home/joe', suffix='some/document.txt')
    'joe/docs/some/document.html'

    >>> tf = TextFiller(r'/goto/{item:/=-:\=_}/')
    >>> tf.fill(item='this/is/slashy')
    '/goto/this-is-slashy/'
    >>> tf.fill(item=r'this\is\back')
    '/goto/this_is_back/'
    >>> tf.fill(item=r'this/is\mixed')
    '/goto/this-is_mixed/'

    >>> tf = TextFiller(r'http://go.to/{item:/=-}, G:{item:/=\}, name: {item}')
    >>> print tf.fill(item='so/me/thing')
    http://go.to/so-me-thing, G:so\me\thing, name: so/me/thing

    >>> tf = TextFiller('{some}/text/to/{fill}')
    >>> tf.fill(some='prefix', badfill='suffix')

    >>> tf = TextFiller('{some/text/to/{fill}')
    >>> tf.is_valid()
    False
    >>> tf.fill(some='prefix', fill='suffix')

    >>> tf = TextFiller('{some}/text/to/{fill:}')
    >>> tf.is_valid()
    False
    >>> tf.fill(some='prefix', fill='suffix')



    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "sect1", {'a': 7, 'bbb': 'xxx', 'c': '-'})
    >>> setconfig_dict(ui, "sect2", {'v': 'vvv'})
    >>> ui.config("sect1", 'a')
    7
    >>> ui.config("sect2", 'v')
    'vvv'


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "pfx-some-sfx": "ala, ma kota",
    ...         "some.nonitem": "bela nie",
    ...         "x": "yes",
    ...         "pfx-other-sfx": 4})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "pfx-some-sfx": "bad",
    ...         "pfx-also-sfx": "too",
    ...         })
    >>>
    >>> for name, value in rgxp_config_items(
    ...         ui, "foo", re.compile(r'^pfx-(\w+)-sfx$')):
    ...    print name, value
    some ala, ma kota
    other 4


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "pfx-some-sfx": "ala, ma kota",
    ...         "some.nonitem": "bela nie",
    ...         "x": "yes",
    ...         "pfx-other-sfx": "sth"})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "pfx-some-sfx": "bad",
    ...         "pfx-also-sfx": "too",
    ...         })
    >>>
    >>> for name, value in rgxp_configlist_items(
    ...         ui, "foo", re.compile(r'^pfx-(\w+)-sfx$')):
    ...    print name, value
    some ['ala', 'ma', 'kota']
    other ['sth']


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "pfx-some-sfx": "true",
    ...         "some.nonitem": "bela nie",
    ...         "x": "yes",
    ...         "pfx-other-sfx": "false"})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "pfx-some-sfx": "1",
    ...         "pfx-also-sfx": "0",
    ...         })
    >>>
    >>> for name, value in rgxp_configbool_items(
    ...         ui, "foo", re.compile(r'^pfx-(\w+)-sfx$')):
    ...    print name, value
    some True
    other False


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "some.item": "ala, ma kota",
    ...         "some.nonitem": "bela nie",
    ...         "x": "yes",
    ...         "other.item": 4})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "some.item": "bad",
    ...         "also.item": "too",
    ...         })
    >>>
    >>> for name, value in suffix_config_items(
    ...         ui, "foo", 'item'):
    ...    print name, value
    some ala, ma kota
    other 4


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "some.item": "ala, ma kota",
    ...         "some.nonitem": "bela nie",
    ...         "x": "yes",
    ...         "other.item": "kazimira"})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "some.item": "bad",
    ...         "also.item": "too",
    ...         })
    >>>
    >>> for name, value in suffix_configlist_items(
    ...         ui, "foo", "item"):
    ...    print name, value
    some ['ala', 'ma', 'kota']
    other ['kazimira']


    >>> import mercurial.ui; ui = mercurial.ui.ui()
    >>> setconfig_dict(ui, "foo", {
    ...         "true.item": "true",
    ...         "false.item": "false",
    ...         "one.item": "1",
    ...         "zero.item": "0",
    ...         "yes.item": "yes",
    ...         "no.item": "no",
    ...         "some.nonitem": "1",
    ...         "x": "yes"})
    >>> setconfig_dict(ui, "notfoo", {
    ...         "some.item": "0",
    ...         "also.item": "too",
    ...         })
    >>>
    >>> for name, value in suffix_configbool_items(
    ...         ui, "foo", "item"):
    ...    print name, str(value)
    zero False
    yes True
    one True
    true True
    no False
    false False
    >>>
    >>> ui.setconfig("foo", "text.item", "something")
    >>> for name, value in suffix_configbool_items(
    ...         ui, "foo", "item"):
    ...    print name, str(value)
    Traceback (most recent call last):
      File "/usr/lib/python2.7/dist-packages/mercurial/ui.py", line 237, in configbool
        % (section, name, v))
    ConfigError: foo.text.item is not a boolean ('something')


    >>> class SomeClass(object):
    ...    def meth(self, arg):
    ...        return "Original: " + arg
    >>>
    >>> @monkeypatch_method(SomeClass)
    ... def meth(self, arg):
    ...     return "Patched: " + meth.orig(self, arg)
    >>>
    >>> obj = SomeClass()
    >>> print obj.meth("some param")
    Patched: Original: some param


    >>> class SomeClass(object):
    ...    def meth(self, arg):
    ...        return "Original: " + arg
    >>>
    >>> @monkeypatch_method(SomeClass, "meth")
    ... def another_meth(self, arg):
    ...     return "Patched: " + another_meth.orig(self, arg)
    >>>
    >>> obj = SomeClass()
    >>> print obj.meth("some param")
    Patched: Original: some param


    >>> import random
    >>> @monkeypatch_function(random)
    ... def seed(x=None):
    ...     print "Forcing random to seed with 0 instead of", x
    ...     return seed.orig(0)
    >>>
    >>> random.seed()
    Forcing random to seed with 0 instead of None
    >>> random.randint(0, 10)
    9

    >>> import random
    >>> @monkeypatch_function(random, 'choice')
    ... def choice_first(sequence):
    ...    return sequence[0]
    >>> for x in range(0, 4): print random.choice("abcdefgh")
    a
    a
    a
    a

'''
